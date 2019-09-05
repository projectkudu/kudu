var newNodeArr = [];
var pendingchildNode = 0;
var nodeArrEntry = function (dateTime, msg, description, objectId, expanded, logURL) {
    return {
        "name": dateTime + " : " + msg,
        "description": description,
        "objectId": objectId,
        "expanded": expanded,
        "logURL": logURL,
        nodes: []
    };
}

function fetchDeploymentInfo(uri) {
    if ((uri === null) || (uri === undefined) || (uri === "")) {
        uri = "/api/deployments/latest/";
    }
    var request = {
        method: "GET"
    };

    return $.ajax(uri, request);
}


//Do not call this passing in the root node's URL as the property names for child and root are different in the output returned from Kudu. Fetch the root separately
function fetchChildNodesFromURL(initialLoad, URI) {

    var deferred = $.Deferred();
    //var self_log_uri = URI;
    if ((URI === null) || (URI === undefined) || (URI === "")) {
        URI = "/api/deployments/latest/";

    }

    fetchDeploymentInfo(URI).done(function (data, textStatus, jqXHR) {
        if (Array.isArray(data) && data.length > 0) {
            var childNodesToAdd = [];
            var log_Url = "";
            var objId = "";
            for (var i = 0; i < data.length; i++) {
                if (data[i].details_url === null || data[i].details_url === undefined)
                    log_Url = "";
                else
                    log_Url = data[i].details_url;
                objId = (data[i].id === "") ? Date.now().toString() : data[i].id;

                if (initialLoad)
                    childNodesToAdd.push(new nodeArrEntry(data[i].log_time, data[i].message, "", objId, false, log_Url));
                else
                    childNodesToAdd.push(new NodeModel(new nodeArrEntry(data[i].log_time, data[i].message, "", objId, false, log_Url)));
            }

            deferred.resolveWith(null, [childNodesToAdd]);
        }
    }).fail(function (jqXHR, textStatus, errorThrown) {
        deferred.rejectWith(null, textStatus, errorThrown);
    });

    return deferred.promise();
}


function updateIf(initialLoad, currEntry, parentNodeDisplayText, parentNodeID, childNodesToAdd) {
    if (initialLoad) {
        if ((currEntry.objectId == parentNodeID) || (currEntry.name == parentNodeDisplayText)) {
            currEntry.nodes = childNodesToAdd;
        }
        else {
            if (Array.isArray(currEntry.nodes) && currEntry.nodes.length > 0) {
                currEntry.nodes.forEach(function (entry) {
                    updateIf(initialLoad, entry, parentNodeDisplayText, parentNodeID, childNodesToAdd);
                });
            }
        }
    }
    else {
        if ((currEntry.objectId() == parentNodeID) || (currEntry.name() == parentNodeDisplayText)) {
            currEntry.nodes(childNodesToAdd);
        }
        else {
            if (Array.isArray(currEntry.nodes()) && currEntry.nodes().length > 0) {
                currEntry.nodes().forEach(function (entry) {
                    updateIf(initialLoad, entry, parentNodeDisplayText, parentNodeID, childNodesToAdd);
                });
            }
        }
    }
}



function addChildNodes(initialLoad, parentNodeDisplayText, parentNodeID, childNodesToAdd) {
    if (initialLoad) {
        newNodeArr.forEach(function (currEntry) {
            updateIf(initialLoad, currEntry, parentNodeDisplayText, parentNodeID, childNodesToAdd);
        });
    }
    else {
        pageModel.NodeModel1.nodes().forEach(function (currEntry) {
            updateIf(initialLoad, currEntry, parentNodeDisplayText, parentNodeID, childNodesToAdd);
        });
    }
}


function updateChildNodes(initialLoad, parentNodeDisplayText, parentNodeID, URI, isRetry) {

    if (!isRetry)
        pendingchildNode++;

    fetchChildNodesFromURL(initialLoad, URI).done(function (childNodes) {
        addChildNodes(initialLoad, parentNodeDisplayText, parentNodeID, childNodes);

        pendingchildNode--;
        if (pendingchildNode < 1) {
            //Done retrieving all child nodes
            if (initialLoad) {
                pageModel.loadData({ navTree: { nodes: newNodeArr } });
            }
            else {
                pageModel.refreshData();
            }
        }

    }).fail(function (textStatus, errorThrown) {
        if (!isRetry) {
            updateChildNodes(initialLoad, parentNodeDisplayText, parentNodeID, URI, true);
            //Try one more time
        }
        pendingchildNode--;
    });
}


function LoadDeploymentLogTree(URI) {

    //Call this only when you need to put new data in the Tree
    newNodeArr = [];
    fetchDeploymentInfo(URI).done(function (data, textStatus, jqXHR) {

        if (data.id) {
            $("#depId").text(data.id);
        }

        var logURL = "";
        if (data.log_url !== null)
            logURL = data.log_url.indexOf("/latest/log") ? data.log_url.replace("latest", data.id) : data.log_url;
        var elem = new nodeArrEntry(data.received_time, data.message, data.status_text, data.id, true, logURL);
        newNodeArr.push(elem);

        if (data.log_url !== "" && data.log_url !== null && data.log_url !== undefined) {
            updateChildNodes(true, elem.name, data.id, logURL, false);
        }
        else {
            //The LogURL for root node is null which means that the deployment just started and no logs have been emitted yet.
            //Retry the load again to ensure that the next time logurl will be present. 
            //In valid scenarios, the root node will always have a log url
            setTimeout(function () {
                //Since this function is supposed to be called only initially, reset the state of the DeploymentLog Tree
                // ClearDeploymentLogTree();
                LoadDeploymentLogTree(URI, false);
            }, 3000);
        }
    }).fail(function (jqXHR, textStatus, errorThrown) {
        console.log("Error : " + textStatus + " --> " + errorThrown);
    });
}

//***************************
//Section Code for handling refresh of the Tree View and maintaining the current expanded state
function isAnyChildExpanded(currNode) {
    var anyImmediateChildExpanded = false;
    for (var i = 0; i < currNode.nodes().length; i++) {
        if (currNode.nodes()[i].expanded()) {
            anyImmediateChildExpanded = true;
            break;
        }
    }
    return anyImmediateChildExpanded;
}

function Refresh_updateIf(currEntry, parentNodeDisplayText, parentNodeID, childNodesToAdd) {
    if ((currEntry.objectId() == parentNodeID) || (currEntry.name() == parentNodeDisplayText)) {
        currEntry.nodes(childNodesToAdd);
    }
    else {
        if (Array.isArray(currEntry.nodes()) && currEntry.nodes().length > 0) {
            currEntry.nodes().forEach(function (entry) {
                Refresh_updateIf(entry, parentNodeDisplayText, parentNodeID, childNodesToAdd);
            });
        }
    }
}

function Refresh_addChildNodes(parentNodeDisplayText, parentNodeID, childNodesToAdd) {

    newArrAfterRefresh.forEach(function (currEntry) {
        Refresh_updateIf(currEntry, parentNodeDisplayText, parentNodeID, childNodesToAdd);
    });

}

function Refresh_appendChildNodesIf(currEntry, parentNodeDisplayText, parentNodeID, childNodesToAdd) {
    if ((currEntry.objectId() == parentNodeID) || (currEntry.name() == parentNodeDisplayText)) {
        //Found a Match, take action here to compare all the nodes and add only the new ones
        if (currEntry.nodes().length != childNodesToAdd.length) {
            if (currEntry.nodes().length < childNodesToAdd.length) {
                //New nodes were detected. Check which ones are new and then add them
                var tempNodesArr = [];
                var isExistingEntry = false;
                for (var i = 0; i < childNodesToAdd.length; i++) {
                    isExistingEntry = false;
                    for (var j = 0; j < currEntry.nodes().length; j++) {
                        if ((childNodesToAdd[i].name() == currEntry.nodes()[j].name()) || (childNodesToAdd[i].objectId() == currEntry.nodes()[j].objectId())) {
                            //This new Node is not present in the existing list. Append it
                            isExistingEntry = true;
                            break;
                        }
                    }
                    if (!isExistingEntry) {
                        tempNodesArr.push(childNodesToAdd[i]);
                    }
                }

                for (var k = 0; k < tempNodesArr.length; k++) {
                    currEntry.nodes().push(tempNodesArr[k]);
                }

            }
            else {
                //Some of the nodes are not present. Remove the ones that the server dropped. 
                //Not implementing this for now as I was not able to repro such a scenario
            }
        }
    }
    else {
        if (Array.isArray(currEntry.nodes()) && currEntry.nodes().length > 0) {
            currEntry.nodes().forEach(function (entry) {
                Refresh_appendChildNodesIf(entry, parentNodeDisplayText, parentNodeID, childNodesToAdd);
            });
        }
    }
}

function appendNewChildren(parentNodeDisplayText, parentNodeID, newchildNodes) {
    //If newchildNodes contains additional nodes, append them to the existing child nodes but do not replace the nodes which were originally present
    newArrAfterRefresh.forEach(function (currEntry) {
        Refresh_appendChildNodesIf(currEntry, parentNodeDisplayText, parentNodeID, newchildNodes);
    });
}

function refreshRecursively(currNode) {
    if (currNode === null || currNode === undefined) {
        //This was called with an empty tree. Happens when deployment is switching from a tempID to an actual DeploymentID. We can ignore this condition. On next refresh, this will work
        return true;
    }
    if (currNode.nodes().length < 1) {
        //This node has no children and can be ignored while processing
        return true;
    }
    else {
        if (!isAnyChildExpanded(currNode)) {
            //This element has child nodes but none of its children are further expanded 
            //So we can refresh this element's children with current data from server safely and then append it to the currentNode
            pendingchildNode++;
            fetchChildNodesFromURL(false, currNode.logURL()).done(function (childNodes) {
                Refresh_addChildNodes(currNode.name(), currNode.objectId(), childNodes);

                pendingchildNode--;


                //Refresh Data Here if pendingchildNode = 0
                if (pendingchildNode < 1) {


                    pageModel.NodeModel1.nodes(newArrAfterRefresh);
                    pageModel.refreshData();
                }
            }).fail(function () {
                pendingchildNode--;
            });
        }
        else {
            //Check to find out which child was expanded and assume that as the new root and call the same function recurvsively again
            for (var i = 0; i < currNode.nodes().length; i++) {
                if (currNode.nodes()[i].expanded()) {
                    refreshRecursively(currNode.nodes()[i]);
                }
            }
            //Have looped through all the child elements now and have made sure that the child nodes that were expanded are now updated with the latest data from the server
            //Now refresh the children of currNode using logURL but retain the the child nodes that have already been updated in the above loop.
            //If additional nodes are added, append them to the existing child nodes but do not replace the nodes which were originally present

            pendingchildNode++;
            fetchChildNodesFromURL(false, currNode.logURL()).done(function (childNodes) {

                appendNewChildren(currNode.name(), currNode.objectId(), childNodes);
                pendingchildNode--;
                //Refresh Data Here if pendingchildNode = 0
                if (pendingchildNode < 1) {


                    pageModel.NodeModel1.nodes(newArrAfterRefresh);
                    pageModel.refreshData();
                }

            }).fail(function () {
                pendingchildNode--;
            });

        }
    }

}

var newArrAfterRefresh = [];


function refreshCurrentTreeData() {
    //Look for Leaf elements only in pageModel.NodeModel1.nodes(). No need to look at the original Array as this function is supposed to be used for Refresh Only

    newArrAfterRefresh = pageModel.NodeModel1.nodes();
    var rootNode = pageModel.NodeModel1.nodes()[0];
    refreshRecursively(rootNode);

    /*
    setTimeout(function () {
        refreshCurrentTreeData();
    }, refreshCurrentTreeDataInterval);
    */

}
//Section Code for handling refresh
//***************************


ko.bindingHandlers.tooltip = {
    init: function (element, valueAccessor) {
        var local = ko.utils.unwrapObservable(valueAccessor()),
            options = {};

        ko.utils.extend(options, local);

        $(element).tooltip(options);

        ko.utils.domNodeDisposal.addDisposeCallback(element, function () {
            $(element).tooltip("destroy");
        });
    }
};


var NodeModel = function (data) {

    var self = this;



    self.description = ko.observable();
    self.name = ko.observable();
    self.nodes = ko.observableArray([]);

    self.toggleVisibility = function () {
        if (self.expanded()) {
            //About to Collapse. Clear the nodes array of this element		
            self.nodes([]);

        }
        else {
            //About to expand now, Populate the nodes array and refresh data inside the done method. Also display a loading glyph
            //Should check if this is a valid URL instead
            if (self.nodes().length > 0) {
                //Refresh the node list here with updated info
                //Implement this only if you decide not to delete the child nodes upon collapsing. Else the code works just fine.
            }
            else {
                //Fetch the Child Node List, this is being expanded for the first time
                if (self.logURL().length > 0) {
                    updateChildNodes(false, self.name(), self.objectId(), self.logURL(), false);

                }
            }

        }

        self.expanded(!self.expanded());
    };


    self.isExpandable = function () {
        return (self.logURL().length > 0);
    };

    self.isExpanded = function () {
        return self.expanded();
    };

    ko.mapping.fromJS(data, self.mapOptions, self);



};

NodeModel.prototype.mapOptions = {
    nodes: {
        create: function (args) {
            return new NodeModel(args.data);
        }
    }
};


var PageModel = function () {

    var self = this;

    self.treeData = ko.observable();

    self.NodeModel1 = null;

    self.loadData = function (data) {
        self.NodeModel1 = new NodeModel(data.navTree);
        self.treeData(self.NodeModel1);

    };

    self.refreshData = function () {
        self.treeData(self.NodeModel1);
    };
}

var pageModel = new PageModel();

function ClearDeploymentLogTree() {
    pageModel.loadData({ navTree: { nodes: [] } });
    ko.applyBindings(pageModel, document.getElementById("depProgress"));
}



ko.bindingHandlers.stopBinding = {
    init: function () {
        return { controlsDescendantBindings: true };
    }
};
ko.virtualElements.allowedBindings.stopBinding = true;


ClearDeploymentLogTree();
LoadDeploymentLogTree();
//setTimeout(function () {
//    refreshCurrentTreeData();
//}, refreshCurrentTreeDataInterval);