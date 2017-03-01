var Utilities = (function () {
    function Utilities() {
    }
    Utilities.toRow = function (name, value) {
        var div = document.createElement("div");
        div.className = "row erow col-s-12";

        var namediv = document.createElement("div");
        namediv.className = "col-xs-4";
        var strong = document.createElement("strong");
        strong.textContent = name ? name.toString() : "NaN";
        namediv.appendChild(strong);

        var valuediv = document.createElement("div");
        valuediv.className = "col-xs-8";
        valuediv.textContent = typeof (value) !== "undefined" ? value.toString() : "NaN";

        div.appendChild(namediv);
        div.appendChild(valuediv);
        return div;
    };

    Utilities.errorDiv = function (value) {
        var div = document.createElement("div");
        div.className = "red-error";
        div.textContent = value;
        return div;
    };

    Utilities.makeDialog = function (jquery, height) {
        return jquery.dialog({
            autoOpen: false,
            width: "auto",
            height: height,
            buttons: {
                "Close": function () {
                    $(this).dialog("close");
                }
            }
        }).css("min-width", 600).css("max-width", 1000);
    };

    Utilities.makeArrayTable = function (id, headers, objects, attachedData) {
        if (typeof attachedData === "undefined") { attachedData = null; }
        var table = document.createElement("table");
        table.id = id;
        table.className = "table table-hover table-condensed";
        var tbody = document.createElement("tbody");
        var trHead = document.createElement("tr");
        for (var i = 0; i < headers.length; i++) {
            var thHead = document.createElement("th");
            thHead.textContent = headers[i];
            trHead.appendChild(thHead);
            tbody.appendChild(trHead);
        }

        for (var i = 0; i < objects.length; i++) {
            var cells = objects[i].tableCells();
            var row = document.createElement("tr");
            for (var j = 0; j < cells.length; j++) {
                var cell = document.createElement("td");
                if (cells[j] instanceof HTMLElement) {
                    cell.appendChild(cells[j]);
                } else {
                    cell.innerHTML = cells[j];
                }
                row.appendChild(cell);
            }
            if (attachedData !== null) {
                $(row).data(attachedData, objects[i]);
            }
            tbody.appendChild(row);
        }
        table.appendChild(tbody);
        return table;
    };

    Utilities.getArrayFromJson = function (jsonArray, action) {
        var array = [];
        for (var i = 0; i < jsonArray.length; i++) {
            array.push(action(jsonArray[i]));
        }
        return array;
    };

    Utilities.getArrayFromJsonObject = function (jsonObject, action) {
        var array = [];
        for (var propertyName in jsonObject) {
            array.push(action(propertyName, jsonObject[propertyName]));
        }
        return array;
    };

    Utilities.createDiv = function (id) {
        var div = document.createElement("div");
        div.id = id;
        return div;
    };

    Utilities.commaSeparateNumber = function (val) {
        var strVal = Math.floor(val).toString(10);
        while (/(\d+)(\d{3})/.test(strVal)) {
            strVal = strVal.replace(/(\d+)(\d{3})/, "$1" + "," + "$2");
        }
        return strVal;
    };

    Utilities.createTabs = function (baseId, tabsHeaders) {
        var tabs = Utilities.createDiv(baseId + "-tabs");

        var ul = document.createElement("ul");

        for (var i = 0; i < tabsHeaders.length; i++) {
            var tab = document.createElement("li");
            var anchor = document.createElement("a");
            anchor.setAttribute("href", "#" + baseId + "-" + tabsHeaders[i].toLowerCase().replace(" ", "-") + "-tab");
            anchor.textContent = tabsHeaders[i];
            tab.appendChild(anchor);
            ul.appendChild(tab);
        }
        tabs.appendChild(ul);
        return $(tabs);
    };

    Utilities.makeSimpleMenu = function (data) {
        var options = {
            selector: "tr",
            trigger: "right",
            callback: function (key) {
                var object = $(this).data(data);
                switch (key) {
                    case "properties":
                        object.dialog().dialog("open");
                        break;
                }
            },
            items: {
                "properties": { name: "Properties" }
            },
            events: {
                hide: function () {
                    $(this).removeClass("selectedMenu");
                },
                show: function () {
                    $(this).addClass("selectedMenu");
                }
            }
        };
        return options;
    };

    Utilities.downloadURL = function (url,showResponseMessage) {
        var hiddenIFrameID = "hiddenDownloader", iframe;
        iframe = document.getElementById(hiddenIFrameID);
        if (iframe === null) {
            iframe = document.createElement("iframe");
            iframe.id = hiddenIFrameID;
            iframe.style.display = "none";
            document.body.appendChild(iframe);
        }
        iframe.onload = function (e) {
            if (showResponseMessage) {
                var iframeDocument = iframe.contentDocument || iframe.contentWindow.document; // for both IE and other
                var iFrameBody = iframeDocument.getElementsByTagName('body')[0];
                var iframeText = $(iFrameBody).text();
                if (iframeText && iframeText.length > 0) {
                    showModal("Error", iframeText);
                }
            }
        }
        iframe.src = url;
   };

    Utilities.arrayToDivs = function (lines) {
        var htmls = [];
        var tmpDiv = jQuery(document.createElement("div"));
        for (var i = 0; i < lines.length; i++) {
            htmls.push(tmpDiv.text(lines[i]).html());
        }
        return htmls.join("<br />");
    };

    Utilities.ToTd = function (value) {
        var td = document.createElement('td');
        if (value instanceof HTMLElement) {
            td.appendChild(value);
        } else {
            td.textContent = value.toString();
        }
        return td;
    };

    Utilities.getButton = function (style, id, textContent, action, addStyle) {
        if (typeof addStyle === "undefined") { addStyle = true; }
        var button = document.createElement("button");
        button.className = style;
        button.id = id;
        button.textContent = textContent;

        $(button).button().click(function (e) {
            $(button).blur();
            action(e);
            $(button).blur();
        });
        if (addStyle) {
            $(button).css("margin-right", "20px");
        }
        return button;
    };


    Utilities.getCheckbox = function (id, textContent, isProfileRunning, iisIisProfileRunning) {
        var span = document.createElement("span");
        $(span).css("width", "138px");
        $(span).css("white-space", "nowrap");
        $(span).css("margin-right", "10px");

        var checkbox = document.createElement("input");
        checkbox.id = id;
        checkbox.type = "checkbox"
        $(checkbox).css("margin-right", "10px");
        $(checkbox).css("margin-left", "10px");

        checkbox.disabled = "";
        checkbox.checked = false;

        if (isProfileRunning)
        {
            checkbox.disabled = "disabled";
        }
        if (iisIisProfileRunning)
        {
            checkbox.checked = true;
        }
       
        span.appendChild(checkbox);

        var label = document.createElement("span");
        label.textContent = textContent;
        span.appendChild(label);

        return span;
    };

    return Utilities;
})();

var Process = (function () {
    var webSiteSku;
    function Process(json) {
        this._json = json;
        this._json.threads = Utilities.getArrayFromJson(json.threads, function (t) {
            return new Thread(t);
        });
        this._json.modules = Utilities.getArrayFromJson(json.modules, function (m) {
            return new Module(m);
        });
        this._json.open_file_handles = Utilities.getArrayFromJson(json.open_file_handles, function (h) {
            return new Handle(h);
        });

        this._json.environment_variables = Utilities.getArrayFromJsonObject(json.environment_variables, function (key, value) {
            if (key === "WEBSITE_SKU") {
                webSiteSku = value;
            }

            return new EnvironmentVariable(key, value);
        });
    }
    Object.defineProperty(Process.prototype, "Id", {
        get: function () {
            return this._json.id;
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "Name", {
        get: function () {
            return this._json.name;
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "FileHandles", {
        get: function () {
            return this._json.open_file_handles;
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "Minidump", {
        get: function () {
            return this._json.minidump;
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "ChildrenIds", {
        get: function () {
            var childrenIds = [];
            var child;
            for (child in this._json.children) {
                childrenIds.push(Process.getIdFromHref(child));
            }
            return childrenIds;
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "ParentId", {
        get: function () {
            return Process.getIdFromHref(this._json.parent);
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "TotalCpuTime", {
        get: function () {
            if (!this._json.total_cpu_time) {
                return "  ?";
            }
            // ts format is [-][d.]hh:mm:ss[.fffffff]
            var total = 0;
            var parts = this._json.total_cpu_time.split(":");
            var hrs = parts[0].split(".");
            if (hrs.length > 1) {
                total += parseInt(hrs[0]) * 24 * 60 * 60;
                total += parseInt(hrs[1]) * 60 * 60;
            } else {
                total += parseInt(hrs[0]) * 60 * 60;
            }
            total += parseInt(parts[1]) * 60;
            total += parseInt(parts[2]);
            if (total !== 0) {
                return "  " + total.toString() + " s";
            } else {
                return "<1 s";
            }
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "UserName", {
        get: function () {
            if (!this._json.user_name) {
                return "  ?";
            }
            var parts = this._json.user_name.split("\\");
            return parts[1];
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "WebSiteSku", {
        get: function () {
            return webSiteSku;
        },
        enumerable: true,
        configurable: true
    });

    Process.prototype.tableRow = function (level) {
        var _this = this;
        var tr = document.createElement('tr');
        tr.setAttribute('data-depth', level.toString());
        tr.className = 'collapsable hoverable';

        var td = document.createElement('td');
        td.style.paddingLeft = (level === 0 ? 5 : level * 30) + 'px';
        var suffix = "";
        if (this._json.is_webjob) {
            suffix = " <span class='label label-info'>webjob</span>";
        } else if (this._json.is_scm_site) {
            suffix = " <span class='label label-primary'>scm</span>";
        }
        if (this._json.children.length > 0) {
            $(td).wrapInner('<span class="toggle"></span>    ' + this.FullName + suffix);
        } else {
            $(td).wrapInner(this.FullName + suffix);
        }
        tr.appendChild(td);
        tr.appendChild(Utilities.ToTd(this._json.id));
        tr.appendChild(Utilities.ToTd(this.UserName));
        tr.appendChild(Utilities.ToTd(this.TotalCpuTime));
        tr.appendChild(Utilities.ToTd(Utilities.commaSeparateNumber(this._json.working_set / 1024) + " KB"));
        tr.appendChild(Utilities.ToTd(Utilities.commaSeparateNumber(this._json.private_memory / 1024) + " KB"));
        tr.appendChild(Utilities.ToTd(Utilities.commaSeparateNumber(this._json.thread_count)));
        tr.appendChild(Utilities.ToTd(Utilities.getButton("btn btn-info", this._json.id + "-properties", "Properties..", function (e) {
            e.preventDefault();
            e.stopPropagation();
            _this.dialog().dialog("open");
            $("li").blur();
        }, false)));
        
        var profilingButton = Utilities.getButton("btn btn-info", this._json.id + "-Profiling", this._json.is_profile_running ? "Stop Profiling" : "Start Profiling", function (e) {
            handleProfilingEvents(e, _this._json.id, _this._json.iis_profile_timeout_in_seconds);
        }, false);
        $(profilingButton).css("width", "138px");

        var iisProfilingCheckbox = Utilities.getCheckbox(this._json.id + "-iisProfilingCheck", "Collect IIS Events", this._json.is_profile_running, this._json.is_iis_profile_running)

        if (this._json.is_profile_running) { // highlight button if the profiler has started
            $(profilingButton).addClass("btn-danger");
        }

        if (Process.prototype.WebSiteSku === "Free" || Process.prototype.WebSiteSku === "Shared") {
            $(profilingButton).css("opacity", "0.5");
            $(profilingButton).off("click");
            $(profilingButton).attr("title", "Profiling is not supported for Free/Shared sites.");
            $(profilingButton).tooltip().show();

            var iisProfilingCheckboxControl = iisProfilingCheckbox.childNodes.item(this._json.id + "-iisProfilingCheck");           
            if (iisProfilingCheckboxControl != null) {
                iisProfilingCheckboxControl.disabled = 'disabled';
            }
        }

        tr.appendChild(Utilities.ToTd(iisProfilingCheckbox));
        tr.appendChild(Utilities.ToTd(profilingButton));
        
        return $(tr);
    };

    Process.prototype.dialog = function () {
        if ($("#" + this._json.id.toString()).length > 0) {
            return $("#" + this._json.id.toString()).tabs("option", "active", 0);
        }

        var div = Utilities.createDiv(this._json.id.toString());
        div.setAttribute("title", this.FullName + ":" + this._json.id + " Properties");

        this.getProcessDatailsTabsHeaders().appendTo(div);

        this.getInfoTab().appendTo(div);
        this.getModulesTab().appendTo(div);
        this.getOpenHandlesTab().appendTo(div);
        this.getThreadsTab().appendTo(div);
        this.getEnvironmentVariablesTab().appendTo(div);

        return Utilities.makeDialog($(div).tabs(), 800);
    };

    Object.defineProperty(Process.prototype, "FullName", {
        get: function () {
            return (this._json.file_name === "N/A" ? this._json.name : this._json.file_name.split("\\").pop());
        },
        enumerable: true,
        configurable: true
    });

    Process.prototype.getOpenHandlesTab = function () {
        var div = Utilities.createDiv(this._json.id.toString() + "-handles-tab");
        var table = Utilities.makeArrayTable(div.id + "-table", ["Handles"], this._json.open_file_handles);
        div.appendChild(table);
        return $(div).hide();
    };

    Process.prototype.getThreadsTab = function () {
        var div = Utilities.createDiv(this._json.id.toString() + "-threads-tab");

        var table = Utilities.makeArrayTable(div.id + "-table", ["Id", "State", "More"], this._json.threads, "thread");
        div.appendChild(table);

        $(table).contextMenu(Utilities.makeSimpleMenu("thread"));

        return $(div).hide();
    };

    Process.prototype.getInfoTab = function () {
        var _this = this;
        var div = Utilities.createDiv(this._json.id.toString() + "-general-tab");

        div.appendChild(Utilities.toRow("id", this._json.id));
        div.appendChild(Utilities.toRow("name", this._json.name));
        div.appendChild(Utilities.toRow("file name", this._json.file_name));
        div.appendChild(Utilities.toRow("command line", this._json.command_line));
        div.appendChild(Utilities.toRow("description", this._json.description ? this._json.description : ""));
        div.appendChild(Utilities.toRow("user name", this._json.user_name));
        div.appendChild(Utilities.toRow("is scm site", this._json.is_scm_site));
        div.appendChild(Utilities.toRow("is webjob", this._json.is_scm_site));
        div.appendChild(Utilities.toRow("handle count", Utilities.commaSeparateNumber(this._json.handle_count)));
        div.appendChild(Utilities.toRow("module countid", Utilities.commaSeparateNumber(this._json.module_count)));
        div.appendChild(Utilities.toRow("thread count", Utilities.commaSeparateNumber(this._json.thread_count)));
        div.appendChild(Utilities.toRow("start time", this._json.start_time));
        div.appendChild(Utilities.toRow("total cpu time", this._json.total_cpu_time));
        div.appendChild(Utilities.toRow("user cpu time", this._json.user_cpu_time));
        div.appendChild(Utilities.toRow("privileged cpu time", this._json.privileged_cpu_time));
        div.appendChild(Utilities.toRow("working set", Utilities.commaSeparateNumber(this._json.working_set / 1024) + " KB"));
        div.appendChild(Utilities.toRow("peak working set", Utilities.commaSeparateNumber(this._json.peak_working_set / 1024) + " KB"));
        div.appendChild(Utilities.toRow("private memory", Utilities.commaSeparateNumber(this._json.private_memory / 1024) + " KB"));
        div.appendChild(Utilities.toRow("virtual memory", Utilities.commaSeparateNumber(this._json.virtual_memory / 1024) + " KB"));
        div.appendChild(Utilities.toRow("peak virtual memory", Utilities.commaSeparateNumber(this._json.peak_virtual_memory / 1024) + " KB"));
        div.appendChild(Utilities.toRow("paged system memory", Utilities.commaSeparateNumber(this._json.paged_system_memory / 1024) + " KB"));
        div.appendChild(Utilities.toRow("non-paged system memory", Utilities.commaSeparateNumber(this._json.non_paged_system_memory / 1024) + " KB"));
        div.appendChild(Utilities.toRow("paged memory", Utilities.commaSeparateNumber(this._json.paged_memory / 1024) + " KB"));
        div.appendChild(Utilities.toRow("peak paged memory", Utilities.commaSeparateNumber(this._json.peak_paged_memory / 1024) + " KB"));

        var buttonDiv = document.createElement("div");
        buttonDiv.className = "buttons-row col-xs-12";

        buttonDiv.appendChild(Utilities.getButton("ui-button-danger", div.id + "-kill", "Kill", function () {
            _this.HTMLElement.removeClass("hoverable");
            _this.HTMLElement.addClass("dying");
            _this.kill().done(function () {
                processExplorerSetupAsync();
                _this.dialog().dialog("close");
            });
        }));

        buttonDiv.appendChild(Utilities.getButton("ui-button-info", div.id + "-dumb", "Download memory dump", function () {
            Utilities.downloadURL(_this._json.minidump);
        }));

        div.appendChild(buttonDiv);

        return $(div).hide();
    };

    Process.prototype.getModulesTab = function () {
        var div = document.createElement("div");
        div.id = this._json.id.toString() + "-modules-tab";

        var table = Utilities.makeArrayTable(div.id + "-table", ["File Name", "File Version", "More"], this._json.modules, "module");
        div.appendChild(table);
        $(table).contextMenu(Utilities.makeSimpleMenu("module"));

        return $(div).hide();
    };

    Process.prototype.getEnvironmentVariablesTab = function () {
        var _this = this;
        var div = Utilities.createDiv(this._json.id.toString() + "-environment-variables-tab");

        var table = Utilities.makeArrayTable(div.id + "-table", ["Key", "Value"], this._json.environment_variables);
        div.appendChild(table);
        return $(div).hide();
    };

    Process.prototype.getProcessDatailsTabsHeaders = function () {
        return Utilities.createTabs(this._json.id.toString(), ["General", "Modules", "Handles", "Threads", "Environment Variables"]);
    };

    Process.prototype.kill = function () {
        return $.ajax({
            url: this._json.href,
            type: "DELETE"
        });
    };

    Process.getIdFromHref = function (href) {
        return parseInt(href.substr(href.lastIndexOf("/") + 1));
    };
    return Process;
})();

var Thread = (function () {
    function Thread(json) {
        this._json = json;
    }
    Thread.prototype.dialog = function () {
        if ($("#" + this._json.id.toString() + "-thread").length > 0) {
            return $("#" + this._json.id.toString() + "-thread");
        }

        var div = document.createElement("div");
        div.id = this._json.id.toString() + "-thread";
        div.setAttribute("title", "Thread " + this._json.id + " Properties");

        this.getInfo().appendTo(div);

        return Utilities.makeDialog($(div), 400);
    };

    Thread.prototype.tableCells = function () {
        var _this = this;
        return [
            this._json.id.toString(), this._json.state, Utilities.getButton("ui-button-info", this._json.id.toString() + "-more", "More...", function () {
                _this.dialog().dialog("open");
            })];
    };

    Thread.prototype.updateSelf = function () {
        var _this = this;
        return $.getJSON(this._json.href, function (response) {
            _this._json = response;
        });
    };

    Thread.prototype.getInfo = function () {
        var _this = this;
        var div = document.createElement("div");
        div.id = this._json.id.toString() + "-info-tab";
        this.updateSelf().done(function () {
            div.appendChild(Utilities.toRow("id", _this._json.id));
            div.appendChild(Utilities.toRow("start address", _this._json.start_address));
            div.appendChild(Utilities.toRow("current priority", _this._json.current_priority));
            div.appendChild(Utilities.toRow("priority_level", _this._json.priority_level));
            div.appendChild(Utilities.toRow("base_priority", _this._json.base_priority));
            div.appendChild(Utilities.toRow("start time", _this._json.start_time));
            div.appendChild(Utilities.toRow("total processor time", _this._json.total_processor_time));
            div.appendChild(Utilities.toRow("user processor time", _this._json.user_processor_time));
            div.appendChild(Utilities.toRow("priviledged processor time", _this._json.priviledged_processor_time));
            div.appendChild(Utilities.toRow("state", _this._json.state));
            div.appendChild(Utilities.toRow("wait reason", _this._json.wait_reason));
        }).fail(function () {
            div.appendChild(Utilities.errorDiv("Couldn't retrive thread details"));
        });

        return $(div);
    };
    return Thread;
})();

var Module = (function () {
    function Module(json) {
        this._json = json;
    }
    Module.prototype.updateSelf = function () {
        var _this = this;
        return $.getJSON(this._json.href, function (response) {
            _this._json = response;
        });
    };

    Module.prototype.tableCells = function () {
        var _this = this;
        return [
            "<strong>" + this._json.file_name + "</strong>", this._json.file_version, Utilities.getButton("ui-button-info", this._json.base_address.toString() + "-more", "More...", function () {
                _this.dialog().dialog("open");
            })];
    };

    Module.prototype.dialog = function () {
        if ($("#" + this._json.base_address.toString() + "-module").length > 0) {
            return $("#" + this._json.base_address.toString() + "-module");
        }

        var div = document.createElement("div");
        div.id = this._json.base_address.toString() + "-module";
        div.setAttribute("title", "module at " + this._json.base_address + " Properties");

        this.getInfo().appendTo(div);

        return Utilities.makeDialog($(div), 400);
    };

    Module.prototype.getInfo = function () {
        var _this = this;
        var div = document.createElement("div");
        div.id = this._json.base_address.toString() + "-module-info-tab";
        this.updateSelf().done(function () {
            div.appendChild(Utilities.toRow("base address", _this._json.base_address));
            div.appendChild(Utilities.toRow("file name", _this._json.file_name));
            div.appendChild(Utilities.toRow("file path", _this._json.file_path));
            div.appendChild(Utilities.toRow("module memory size", _this._json.module_memory_size));
            div.appendChild(Utilities.toRow("file version", _this._json.file_version));
            div.appendChild(Utilities.toRow("file description", _this._json.file_description));
            div.appendChild(Utilities.toRow("product", _this._json.product));
            div.appendChild(Utilities.toRow("product version", _this._json.product_version));
            div.appendChild(Utilities.toRow("is debug", _this._json.is_debug));
            div.appendChild(Utilities.toRow("language", _this._json.language));
        }).fail(function () {
            div.appendChild(Utilities.errorDiv("Couldn't retrive module details"));
        });

        return $(div);
    };
    return Module;
})();

var Handle = (function () {
    function Handle(fileName) {
        this.file_name = fileName;
    }
    Handle.prototype.dialog = function () {
        throw "Not Implemented";
    };

    Handle.prototype.tableCells = function () {
        return [this.file_name];
    };

    Handle.prototype.updateSelf = function () {
        throw "Not Implemented";
    };
    return Handle;
})();

var EnvironmentVariable = (function () {
    function EnvironmentVariable(key, value) {
        this.key = key;
        this.value = value;
    }
    EnvironmentVariable.prototype.dialog = function () {
        throw "Not Implemented";
    };

    EnvironmentVariable.prototype.tableCells = function () {
        return [this.key, this.value];
    };

    EnvironmentVariable.prototype.updateSelf = function () {
        throw "Not Implemented";
    };
    return EnvironmentVariable;
})();

var Tree = (function () {
    function Tree() {
        this.roots = [];
    }
    Tree.prototype.contains = function (pid) {
        for (var i = 0; i < this.roots.length; i++) {
            if (Tree.recursiveContains(this.roots[i], pid)) {
                return true;
            }
        }
        return false;
    };

    Tree.prototype.buildTree = function (nodeList) {
        nodeList.sort(function (a, b) {
            return a.process.Id - b.process.Id;
        });
        this.roots.sort(function (a, b) {
            return a.process.Id - b.process.Id;
        });
        for (var i = 0; i < this.roots.length; i++) {
            Tree.addChildren(this.roots[i], nodeList);
        }
        $(".collapsable").remove();
        $(".expandable").remove();
        for (var i = 0; i < this.roots.length; i++) {
            Tree.printTreeTable(this.roots[i], 0, $("#proctable"));
        }
    };

    Tree.recursiveContains = function (node, pid) {
        if (node.process.Id === pid) {
            return true;
        } else {
            for (var i = 0; i < node.children.length; i++) {
                if (Tree.recursiveContains(node[i], pid)) {
                    return true;
                }
            }
        }
        return false;
    };

    Tree.addChildren = function (node, nodeList) {
        for (var i = 0; i < nodeList.length; i++) {
            if (nodeList[i].process.ParentId === node.process.Id) {
                node.children.push(nodeList[i]);
            }
        }

        for (var i = 0; i < node.children.length; i++) {
            Tree.addChildren(node.children[i], nodeList);
        }
    };

    Tree.printTreeTable = function (node, level, tableRoot) {
        var jcurrent = node.process.tableRow(level);
        jcurrent.data("proc", node.process).appendTo(tableRoot);
        node.process.HTMLElement = jcurrent;
        for (var i = 0; i < node.children.length; i++) {
            Tree.printTreeTable(node.children[i], level + 1, tableRoot);
        }
    };

    //debug method leave it
    Tree.printTreeUl = function (node, parent) {
        var current = "<li><span>";
        current += "(" + node.process.Id + ") " + node.process.Name;
        current += "</span></li>";
        var jcurrent = $(current).appendTo(parent);
        if (node.children.length > 0) {
            jcurrent = $("<ul></ul>").appendTo(jcurrent);
            for (var i = 0; i < node.children.length; i++) {
                Tree.printTreeUl(node.children[i], jcurrent);
            }
        }
    };

    //debug method leave it
    Tree.printTreeConsole = function (node, level) {
        var indentation = "";
        for (var i = 0; i < level - 1; i++) {
            indentation += "    ";
        }
        if (indentation.length != 0 || level > 0)
            indentation += "|__>";
        console.log(indentation + "(" + node.process.Id + ") " + node.process.Name);
        for (var i = 0; i < node.children.length; i++) {
            Tree.printTreeConsole(node.children[i], level + 1);
        }
    };
    return Tree;
})();

var ProcessNode = (function () {
    function ProcessNode(process) {
        this.process = process;
        this.children = [];
    }
    return ProcessNode;
})();

var nodeList;

function processExplorerSetupAsync() {
    $("#proc-loading").show();
    var processTree = new Tree();
    nodeList = [];
    var deferred = [];
    $.getJSON(appRoot + "api/processes", function (data) {
        var ids = $.map(data, function (item, index) {
            return item.id;
        });
        for (var i = 0; i < data.length; i++) {
            deferred.push($.getJSON(data[i].href, function (response) {
                var p = new Process(response);
                var processNode = new ProcessNode(p);
                if (p.ParentId === -1 || $.inArray(p.ParentId, ids) < 0) {
                    processTree.roots.push(processNode);
                }
                nodeList.push(new ProcessNode(p));
            }));
        }
    }).done(function () {
        return $.whenAll.apply($, deferred).then(function () {
            return processTree.buildTree(nodeList);
        }, function () {
            return processTree.buildTree(nodeList);
        }).always(function () {
            return $("#proc-loading").hide();
        });
    });
}

function enableCollabsableNodes() {
    //http://stackoverflow.com/questions/5636375/how-to-create-a-collapsing-tree-table-in-html-css-js
    $("#proctable").on("click", ".toggle", function (e) {
        e.preventDefault();
        e.stopPropagation();

        //Gets all <tr>"s  of greater depth
        //below element in the table
        //TODO: change back to tr if there is an issue
        var findChildren = function (_tr) {
            var depth = _tr.data("depth");
            return _tr.nextUntil($("tr").filter(function () {
                return $(this).data("depth") <= depth;
            }));
        };

        var el = $(this);
        var tr = el.closest("tr");
        var children = findChildren(tr);

        //Remove already collapsed nodes from children so that we don"t
        //make them visible.
        var subnodes = children.filter(".expandable");
        subnodes.each(function () {
            var subnode = $(this);
            var subnodeChildren = findChildren(subnode);
            children = children.not(subnodeChildren);
        });

        //Change icon and hide/show children
        if (tr.hasClass("collapsable")) {
            tr.removeClass("collapsable").addClass("expandable");
            children.hide();
        } else {
            tr.removeClass("expandable").addClass("collapsable");
            children.show();
        }
        return children;
    });
}

function overrideRightClickMenu() {
    var options = {
        selector: "tr",
        trigger: "right",
        callback: function (key) {
            var process = $(this).data("proc");
            switch (key) {
                case "kill":
                    $(this).removeClass("hoverable");
                    $(this).addClass("dying");
                    process.kill().done(function () {
                        return processExplorerSetupAsync();
                    }).fail(function () {
                        return processExplorerSetupAsync();
                    });
                    break;
                case "dump1":
                    Utilities.downloadURL(process.Minidump + "?dumpType=1");
                    break;
                case "dump2":
                    Utilities.downloadURL(process.Minidump + "?dumpType=2");
                    break;
                case "properties":
                    process.dialog().dialog("open");
                    $("li")[0].click();
                    $("li").blur();
                    break;
            }
        },
        items: {
            "kill": { name: "Kill" },
            "dump": {
                name: "Download Memory Dump",
                "items": {
                    "dump1": { name: "Mini Dump" },
                    "dump2": { name: "Full Dump" }
                }
            },
            "sep1": "---------",
            "properties": { name: "Properties" }
        },
        events: {
            hide: function () {
                $(this).removeClass("selectedMenu");
            },
            show: function () {
                $(this).addClass("selectedMenu");
            }
        }
    };
    $("#proctable").contextMenu(options);
}

function searchForHandle() {
    var name = $("#name").val().toLowerCase();
    var result = [];
    for (var i = 0; i < nodeList.length; i++) {
        for (var j = 0; j < nodeList[i].process.FileHandles.length; j++) {
            var check = nodeList[i].process.FileHandles[j].file_name.replace(/\\+$/, "").toLowerCase();
            check = check.substring(check.lastIndexOf("\\"));
            if (check.indexOf(name) !== -1) {
                result.push(nodeList[i].process.FullName + ":" + nodeList[i].process.Id + " -> " + nodeList[i].process.FileHandles[j].file_name);
            }
        }
    }
    if (result.length > 0) {
        $("#handle-result").html(Utilities.arrayToDivs(result));
    } else {
        $("#handle-result").html(Utilities.errorDiv("No handle found").outerHTML);
    }
}

function handleProfilingEvents(e, processId, iisProfilingTimeoutInSeconds) {
    if (!e || !e.target || typeof e.target.textContent === "undefined") {
        return;
    }

    var iisProfiling = false;

    var iisProfilingCheckbox = document.getElementById(processId + "-iisProfilingCheck");

    if (iisProfilingCheckbox != null) {
        iisProfiling = iisProfilingCheckbox.checked;
    }

    if (e.target.textContent.indexOf("Stop") === 0) {
        stopProfiling(e, processId);
        if (iisProfilingCheckbox != null) {
            iisProfilingCheckbox.disabled = "";
        }
    }
    else if (e.target.textContent.indexOf("Starting") !== 0) {
        if (iisProfiling) {
            var divConfirm = document.getElementById('dialog-confirm')
            if (divConfirm == null) {
                divConfirm = Utilities.createDiv('dialog-confirm');
                divConfirm.title = "Collect IIS Events?";

            }
            divConfirm.innerHTML = "IIS profiling enables IIS and threadTime ETW events. The generated trace file can be analyzed using Perfview which is available at <a style='outline: none' href='https://www.microsoft.com/en-us/download/details.aspx?id=28567'>https://www.microsoft.com/en-us/download/details.aspx?id=28567</a>. <br/><br/>The profiling session will automatically timeout after <b>" + iisProfilingTimeoutInSeconds.toString() + "</b> seconds if not stopped manually.<br/><br/>Enabling IIS Profiling is a relatively <b>expensive option</b> to turn on as it increases the CPU usage and disk I/O on your instance. Are you sure you want to continue?"
            $(divConfirm).dialog({
                resizable: false,
                height: 330,
                width: 450,
                modal: true,
                buttons: [{
                    text: "Yes",
                    click: function () {
                        if (iisProfilingCheckbox != null) {
                            iisProfilingCheckbox.disabled = "disabled";
                        }
                        e.target.textContent = "Starting Profiler...";
                        startProfiling(e, processId, iisProfiling);
                        $(this).dialog("close");
                    }
                }, {
                    text: "No",
                    click: function () {
                        $(this).dialog("close");
                    }
                }]
            });
        }
        else {

            // so this is when someone wants to collect profiler traces
            // without the IIS profiling events. We still want to
            // disable the profiling check box here

            if (iisProfilingCheckbox != null) {
                iisProfilingCheckbox.disabled = "disabled";
            }
            e.target.textContent = "Starting Profiler...";
            startProfiling(e, processId, iisProfiling);
        }
    }
}

function postProfileRequest(action, processId, iisProfiling) {
    var uri = "/api/processes/" + processId + "/profile/" + action;

    if (iisProfiling)
        uri = uri + "?iisProfiling=true";

    var request = {
            method: "POST",
            contentType: "application/json",
        };
    return $.ajax(uri, request);
}

function startProfiling(e, processId, iisProfiling) {
    var request = postProfileRequest("start", processId, iisProfiling);
    request.done(function (resp) {
        e.target.title = "";
        e.target.textContent = "Stop Profiling";
        $(e.currentTarget).addClass("btn-danger");  // highlight button if the profiler has started
    });
    request.fail(function (resp) {
        var obj = JSON.parse(resp.responseText);
        alert(obj["Message"]);
        e.target.textContent = "Start Profiling";
    });
}

function showModal(title, content) {

    if (!$("#generalModal").hasClass('ui-dialog-content')) { //check if already init-ed
        $("#generalModal").dialog({
            autoOpen: false,
            resizable: false,
            draggable: false,
            modal: true,
            width: "500px",
            open: function () {
                $(this).siblings(".ui-dialog-titlebar")
                              .find("button").blur();
            },
            create: function () {
                $(".ui-dialog").find(".ui-dialog-titlebar").css({
                    'background-image': 'none',
                    'background-color': 'white',
                    'border': 'none'
                });
            }
        });
    }
    $('#generalModal').dialog('option', 'title', title);
    $("#generalModal .content").text(content);
    $("#generalModal").dialog("open");
}

function stopProfiling(e, processId) {
    var uri = window.location.protocol + "//" + window.location.host + "/api/processes/" + processId + "/profile/stop";
    showModal("Generating diagnostics...", "The profiler is now generating your report. This could take a couple of minutes after which you will be prompted with a download.");

    Utilities.downloadURL(uri, true);
    e.target.textContent = "Start Profiling";
    e.target.title = "It may take a few seconds for the download profile dialog to appear";
    
    $(e.currentTarget).removeClass("btn-danger");  // remove highlight button if the profiler has stopped

    var iisProfilingCheckbox = document.getElementById(processId + "-iisProfilingCheck");
    if (iisProfilingCheckbox != null) {
        iisProfilingCheckbox.disabled = "";
        iisProfilingCheckbox.checked = false; 
    }
}

window.onload = function () {
    //http://stackoverflow.com/questions/5518181/jquery-deferreds-when-and-the-fail-callback-arguments
    $.whenAll = function (firstParam) {
        var args = arguments, sliceDeferred = [].slice, i = 0, length = args.length, count = length, rejected, deferred = length <= 1 && firstParam && jQuery.isFunction(firstParam.promise) ? firstParam : jQuery.Deferred();

        function resolveFunc(i, reject) {
            return function (value) {
                rejected |= reject;
                args[i] = arguments.length > 1 ? sliceDeferred.call(arguments, 0) : value;
                if (!(--count)) {
                    // Strange bug in FF4:
                    // Values changed onto the arguments object sometimes end up as undefined values
                    // outside the $.when method. Cloning the object into a fresh array solves the issue
                    var fn = rejected ? deferred.rejectWith : deferred.resolveWith;
                    fn.call(deferred, deferred, sliceDeferred.call(args, 0));
                }
            };
        }

        if (length > 1) {
            for (; i < length; i++) {
                if (args[i] && jQuery.isFunction(args[i].promise)) {
                    args[i].promise().then(resolveFunc(i, false), resolveFunc(i, true));
                } else {
                    --count;
                }
            }
            if (!count) {
                deferred.resolveWith(deferred, args);
            }
        } else if (deferred !== firstParam) {
            deferred.resolveWith(deferred, length ? [firstParam] : []);
        }
        return deferred.promise();
    };

    $("#find-file-handle").button().click(function () {
        $("#dialog-form").dialog("open");
    });

    $("#dialog-form").dialog({
        autoOpen: false,
        height: 300,
        width: 800,
        buttons: {
            "Search": function () {
                return searchForHandle();
            },
            Cancel: function () {
                $(this).dialog("close");
            }
        }
    });

    $("#dialog-form").keypress(function (e) {
        if (e.keyCode === $.ui.keyCode.ENTER) {
            e.preventDefault();
            e.stopPropagation();
            searchForHandle();
        }
    });

    processExplorerSetupAsync();
    enableCollabsableNodes();
    overrideRightClickMenu();
};
