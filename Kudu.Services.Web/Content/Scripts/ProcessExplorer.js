function ToRow(name, value) {
    var div = document.createElement('div');
    div.className = 'erow col-xs-12';

    var namediv = document.createElement('div');
    namediv.className = 'col-xs-5';
    var strong = document.createElement('strong');
    strong.textContent = name ? name.toString() : 'NaN';
    namediv.appendChild(strong);

    var valuediv = document.createElement('div');
    valuediv.textContent = value ? value.toString() : 'NaN';

    div.appendChild(namediv);
    div.appendChild(valuediv);
    return div;
}

function ErrorDiv(value) {
    var div = document.createElement('div');
    div.className = 'red-error';
    div.textContent = value;
    return div;
}

var Process = (function () {
    function Process() {
    }
    Object.defineProperty(Process.prototype, "ChildrenIds", {
        get: function () {
            var childrenIds = [];
            var child;
            for (child in this.children) {
                childrenIds.push(Process.getIdFromHref(child));
            }
            return childrenIds;
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "ParentId", {
        get: function () {
            return Process.getIdFromHref(this.parent);
        },
        enumerable: true,
        configurable: true
    });

    Object.defineProperty(Process.prototype, "TotalCpuTime", {
        get: function () {
            if (!this.total_cpu_time) {
                return '  ?';
            }
            var total = 0;
            var parts = this.total_cpu_time.split(':');
            total += parseInt(parts[0]) * 60;
            total += parseInt(parts[1]) * 60;
            total += parseInt(parts[2]);
            if (total !== 0) {
                return '  ' + total.toString() + ' s';
            } else {
                return '<1 s';
            }
        },
        enumerable: true,
        configurable: true
    });

    Process.prototype.Dialog = function () {
        if ($('#' + this.id.toString()).length > 0) {
            return $('#' + this.id.toString());
        }

        var div = document.createElement('div');
        div.id = this.id.toString();
        div.setAttribute('title', this.FullName + ':' + this.id + ' Properties');

        this.getProcessDatailsTabsHeaders().appendTo(div);

        this.getInfoTab().appendTo(div);

        this.getOpenHandlesTab().appendTo(div);
        this.getThreadsTab().appendTo(div);

        return $(div).tabs().dialog({
            autoOpen: false,
            width: 600,
            height: 800,
            buttons: {
                'Ok': function () {
                    $(this).dialog('close');
                },
                'Cancel': function () {
                    $(this).dialog('close');
                }
            }
        });
    };

    Object.defineProperty(Process.prototype, "FullName", {
        get: function () {
            return (this.file_name === 'N/A' ? this.name : this.file_name.split('\\').pop());
        },
        enumerable: true,
        configurable: true
    });

    Process.prototype.getOpenHandlesTab = function () {
        var div = document.createElement('div');
        div.id = this.id.toString() + '-handles-tab';

        var table = document.createElement('table');
        table.id = div.id + '-table';
        table.className = 'table table-hover table-condensed';
        var tbody = document.createElement('tbody');
        var trHead = document.createElement('tr');
        var thHead = document.createElement('th');
        thHead.textContent = 'File Handles';
        trHead.appendChild(thHead);
        tbody.appendChild(trHead);
        for (var i = 0; i < this.open_file_handles.length; i++) {
            var handleRow = document.createElement('tr');
            var handleCell = document.createElement('td');
            handleCell.textContent = this.open_file_handles[i];
            handleRow.appendChild(handleCell);
            tbody.appendChild(handleRow);
        }
        table.appendChild(tbody);
        div.appendChild(table);
        return $(div).hide();
    };

    Process.prototype.getThreadsTab = function () {
        var div = document.createElement('div');
        div.id = this.id.toString() + '-threads-tab';

        var table = document.createElement('table');
        table.id = div.id + '-table';
        table.className = 'table table-hover table-condensed';
        var tbody = document.createElement('tbody');
        var trHead = document.createElement('tr');
        var thHead = document.createElement('th');
        thHead.textContent = 'Id';
        trHead.appendChild(thHead);

        thHead = document.createElement('th');
        thHead.textContent = 'State';
        trHead.appendChild(thHead);

        tbody.appendChild(trHead);
        for (var i = 0; i < this.threads.length; i++) {
            var threadRow = document.createElement('tr');
            var threadCell = document.createElement('td');
            threadCell.textContent = this.threads[i].id.toString();
            threadRow.appendChild(threadCell);

            threadCell = document.createElement('td');
            threadCell.textContent = this.threads[i].state;
            threadRow.appendChild(threadCell);
            $(threadRow).data('thread', this.threads[i]);
            tbody.appendChild(threadRow);
        }
        table.appendChild(tbody);
        div.appendChild(table);

        var options = {
            selector: 'tr',
            trigger: 'both',
            callback: function (key) {
                var thread = $(this).data('thread');
                switch (key) {
                    case 'properties':
                        thread.Dialog().dialog('open');
                        break;
                }
            },
            items: {
                'properties': { name: 'Properties' }
            },
            events: {
                hide: function () {
                    $(this).removeClass('selectedMenu');
                },
                show: function () {
                    $(this).addClass('selectedMenu');
                }
            }
        };
        $(table).contextMenu(options);

        return $(div).hide();
    };

    Process.prototype.getInfoTab = function () {
        var _this = this;
        var div = document.createElement('div');
        div.id = this.id.toString() + '-info-tab';

        div.appendChild(ToRow('id', this.id));
        div.appendChild(ToRow('name', this.name));
        div.appendChild(ToRow('file name', this.file_name));
        div.appendChild(ToRow('handle count', commaSeparateNumber(this.handle_count)));
        div.appendChild(ToRow('module countid', commaSeparateNumber(this.module_count)));
        div.appendChild(ToRow('thread count', commaSeparateNumber(this.thread_count)));
        div.appendChild(ToRow('start time', this.start_time));
        div.appendChild(ToRow('total cpu time', this.total_cpu_time));
        div.appendChild(ToRow('user cpu time', this.user_cpu_time));
        div.appendChild(ToRow('privileged cpu time', this.privileged_cpu_time));
        div.appendChild(ToRow('working set', commaSeparateNumber(this.working_set / 1024) + ' K'));
        div.appendChild(ToRow('peak working set', commaSeparateNumber(this.peak_working_set / 1024) + ' K'));
        div.appendChild(ToRow('private memory', commaSeparateNumber(this.private_memory / 1024) + ' K'));
        div.appendChild(ToRow('virtual memory', commaSeparateNumber(this.virtual_memory / 1024) + ' K'));
        div.appendChild(ToRow('peak virtual memory', commaSeparateNumber(this.peak_virtual_memory / 1024) + ' K'));
        div.appendChild(ToRow('paged system memory', commaSeparateNumber(this.paged_system_memory / 1024) + ' K'));
        div.appendChild(ToRow('non-paged system memory', commaSeparateNumber(this.non_paged_system_memory / 1024) + ' K'));
        div.appendChild(ToRow('paged memory', commaSeparateNumber(this.paged_memory / 1024) + ' K'));
        div.appendChild(ToRow('peak paged memory', commaSeparateNumber(this.peak_paged_memory / 1024) + ' K'));

        var buttonDiv = document.createElement('div');
        buttonDiv.className = 'buttons-row col-xs-12';

        buttonDiv.appendChild(Process.getButton('ui-button-danger', div.id + '-kill', 'Kill', function () {
            _this.HTMLElement.removeClass('hoverable');
            _this.HTMLElement.addClass('dying');
            _this.kill().done(function () {
                processExplorerSetupAsync();
                _this.Dialog().dialog('close');
            });
        }));

        buttonDiv.appendChild(Process.getButton('ui-button-info', div.id + '-dumb', 'Download memory dump', function () {
            downloadURL(_this.minidump + '?dumpType=1');
        }));

        buttonDiv.appendChild(Process.getButton('ui-button-info', div.id + '-gcdumb', 'Download GC dump', function () {
            downloadURL(_this.gcdump);
        }));

        div.appendChild(buttonDiv);

        return $(div).hide();
    };

    Process.prototype.getProcessDatailsTabsHeaders = function () {
        var tabs = document.createElement('div');
        tabs.id = this.id.toString() + '-tabs';

        var ul = document.createElement('ul');

        var infotab = document.createElement('li');
        var anchor = document.createElement('a');
        anchor.setAttribute('href', '#' + this.id.toString() + '-info-tab');
        anchor.textContent = 'General';
        infotab.appendChild(anchor);

        var handlestab = document.createElement('li');
        anchor = document.createElement('a');
        anchor.setAttribute('href', '#' + this.id.toString() + '-handles-tab');
        anchor.textContent = 'File Handles';
        handlestab.appendChild(anchor);

        var threadstab = document.createElement('li');
        anchor = document.createElement('a');
        anchor.setAttribute('href', '#' + this.id.toString() + '-threads-tab');
        anchor.textContent = 'Threads';
        threadstab.appendChild(anchor);

        ul.appendChild(infotab);
        ul.appendChild(handlestab);
        ul.appendChild(threadstab);

        tabs.appendChild(ul);

        return $(tabs);
    };

    Process.prototype.kill = function () {
        return $.ajax({
            url: this.href,
            type: 'DELETE'
        });
    };

    Process.getButton = function (style, id, textContent, action) {
        var button = document.createElement('button');
        button.className = style;
        button.id = id;
        button.textContent = textContent;

        $(button).button().click(function () {
            action();
            $(button).blur();
        }).css('margin-right', '20px');
        return button;
    };

    Process.getIdFromHref = function (href) {
        return parseInt(href.substr(href.lastIndexOf('/') + 1));
    };

    Process.fromJson = function (json) {
        var result = new Process();
        result.id = json.id;
        result.name = json.name;
        result.href = json.href;
        result.minidump = json.minidump;
        result.gcdump = json.gcdump;
        result.parent = json.parent;
        result.children = json.children;
        result.threads = Process.getThreadsFromJason(json.threads);
        result.open_file_handles = json.open_file_handles;
        result.file_name = json.file_name;
        result.handle_count = json.handle_count;
        result.module_count = json.module_count;
        result.thread_count = json.thread_count;
        result.start_time = json.start_time;
        result.total_cpu_time = json.total_cpu_time;
        result.user_cpu_time = json.user_cpu_time;
        result.privileged_cpu_time = json.privileged_cpu_time;
        result.working_set = json.working_set;
        result.peak_working_set = json.peak_working_set;
        result.private_memory = json.private_memory;
        result.virtual_memory = json.virtual_memory;
        result.peak_virtual_memory = json.peak_virtual_memory;
        result.paged_system_memory = json.paged_system_memory;
        result.non_paged_system_memory = json.non_paged_system_memory;
        result.paged_memory = json.paged_memory;
        result.peak_paged_memory = json.peak_paged_memory;
        return result;
    };

    Process.getThreadsFromJason = function (jsonArray) {
        var threads = [];
        for (var i = 0; i < jsonArray.length; i++) {
            threads.push(Thread.fromJson(jsonArray[i]));
        }
        return threads;
    };

    Process.fromJsonString = function (json) {
        return Process.fromJson(JSON.parse(json));
    };
    return Process;
})();

var Thread = (function () {
    function Thread() {
    }
    Thread.prototype.Dialog = function () {
        if ($('#' + this.id.toString() + '-thread').length > 0) {
            return $('#' + this.id.toString());
        }

        var div = document.createElement('div');
        div.id = this.id.toString();
        div.setAttribute('title', 'Thread ' + this.id + ' Properties');

        this.getInfo().appendTo(div);

        return $(div).dialog({
            autoOpen: false,
            width: 500,
            height: 400,
            buttons: {
                'Ok': function () {
                    $(this).dialog('close');
                },
                'Cancel': function () {
                    $(this).dialog('close');
                }
            }
        });
    };

    Thread.prototype.updateSelf = function () {
        var _this = this;
        return $.getJSON(this.href, function (response) {
            Thread.fillThreadObj(response, _this);
        });
    };

    Thread.prototype.getInfo = function () {
        var _this = this;
        var div = document.createElement('div');
        div.id = this.id.toString() + '-info-tab';
        this.updateSelf().done(function () {
            div.appendChild(ToRow('id', _this.id));
            div.appendChild(ToRow('start address', _this.start_address));
            div.appendChild(ToRow('current priority', _this.current_priority));
            div.appendChild(ToRow('priority_level', _this.priority_level));
            div.appendChild(ToRow('base_priority', _this.base_priority));
            div.appendChild(ToRow('start time', _this.start_time));
            div.appendChild(ToRow('total processor time', _this.total_processor_time));
            div.appendChild(ToRow('user processor time', _this.user_processor_time));
            div.appendChild(ToRow('priviledged processor time', _this.priviledged_processor_time));
            div.appendChild(ToRow('state', _this.state));
            div.appendChild(ToRow('wait reason', _this.wait_reason));
        }).fail(function () {
            div.appendChild(ErrorDiv('Couldn\'t retrive thread details'));
        });

        return $(div);
    };

    Thread.fromJson = function (json) {
        var result = new Thread();
        Thread.fillThreadObj(json, result);
        return result;
    };

    Thread.fillThreadObj = function (source, target) {
        target.id = source.id;
        target.href = source.href;
        target.process = source.process;
        target.start_address = source.start_address;
        target.current_priority = source.current_priority;
        target.priority_level = source.priority_level;
        target.base_priority = source.base_priority;
        target.start_time = source.start_time;
        target.total_processor_time = source.total_processor_time;
        target.user_processor_time = source.user_processor_time;
        target.priviledged_processor_time = source.priviledged_processor_time;
        target.state = source.state;
        target.wait_reason = source.wait_reason;
    };

    Thread.fromJsonString = function (json) {
        return Process.fromJson(JSON.parse(json));
    };
    return Thread;
})();

var Handle = (function () {
    function Handle() {
    }
    return Handle;
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

    Tree.recursiveContains = function (node, pid) {
        if (node.process.id === pid) {
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
    return Tree;
})();

var ProcessNode = (function () {
    function ProcessNode(process) {
        this.process = process;
        this.children = [];
    }
    return ProcessNode;
})();

function getProcess(href) {
    return Process.fromJsonString($.ajax({
        type: 'GET',
        url: href,
        dataType: 'json',
        success: function () {
        },
        data: {},
        async: false
    }).responseText);
}

function addChildren(node, nodeList) {
    for (var i = 0; i < nodeList.length; i++) {
        if (nodeList[i].process.ParentId === node.process.id) {
            node.children.push(nodeList[i]);
        }
    }

    for (var i = 0; i < node.children.length; i++) {
        addChildren(node.children[i], nodeList);
    }
}

function commaSeparateNumber(val) {
    var strVal = Math.floor(val).toString(10);
    while (/(\d+)(\d{3})/.test(strVal)) {
        strVal = strVal.replace(/(\d+)(\d{3})/, '$1' + ',' + '$2');
    }
    return strVal;
}

//debug method leave it
function printTreeUl(node, parent) {
    var current = '<li><span>';
    current += '(' + node.process.id + ') ' + node.process.name;
    current += '</span></li>';
    var jcurrent = $(current).appendTo(parent);
    if (node.children.length > 0) {
        jcurrent = $('<ul></ul>').appendTo(jcurrent);
        for (var i = 0; i < node.children.length; i++) {
            printTreeUl(node.children[i], jcurrent);
        }
    }
}

function ToTd(value) {
    var td = document.createElement('td');
    td.textContent = value.toString();
    return td;
}

function printTreeTable(node, level, tableRoot) {
    var current = '<tr data-depth="' + level + '" class="collapsable hoverable">';
    current += '<td style="padding-left: ' + (level === 0 ? 5 : level * 30) + 'px">' + (node.children.length > 0 ? '<span class="toggle"></span>   ' : '') + node.process.FullName + '</td>';
    current += '<td>' + node.process.id + '</td>';
    current += '<td>' + node.process.TotalCpuTime + '</td>';
    current += '<td>' + commaSeparateNumber(node.process.working_set / 1024) + ' K</td>';
    current += '<td>' + commaSeparateNumber(node.process.private_memory / 1024) + ' K</td>';
    current += '<td>' + commaSeparateNumber(node.process.thread_count) + '</td>';
    current += '</tr>';
    var jcurrent = $(current);
    jcurrent.data('proc', node.process).appendTo(tableRoot);
    node.process.HTMLElement = jcurrent;
    for (var i = 0; i < node.children.length; i++) {
        printTreeTable(node.children[i], level + 1, tableRoot);
    }
}

//debug method leave it
function printTreeConsole(node, level) {
    var indentation = '';
    for (var i = 0; i < level - 1; i++) {
        indentation += '    ';
    }
    if (indentation.length != 0 || level > 0)
        indentation += '|__>';
    console.log(indentation + '(' + node.process.id + ') ' + node.process.name);
    for (var i = 0; i < node.children.length; i++) {
        printTreeConsole(node.children[i], level + 1);
    }
}

var nodeList;

function processExplorerSetupAsync() {
    $('#proc-loading').show();
    var processTree = new Tree();
    nodeList = [];
    var deferred = [];
    $.getJSON('/diagnostics/processes', function (data) {
        for (var i = 0; i < data.length; i++) {
            deferred.push($.getJSON(data[i].href, function (response) {
                var p = Process.fromJson(response);
                var processNode = new ProcessNode(p);
                if (p.ParentId === -1) {
                    processTree.roots.push(processNode);
                }
                nodeList.push(new ProcessNode(p));
            }));
        }
    }).done(function () {
        return $.whenAll.apply($, deferred).then(function () {
            return buildTree(processTree);
        }, function () {
            return buildTree(processTree);
        }).always(function () {
            return $('#proc-loading').hide();
        });
    });
}

function buildTree(processTree) {
    nodeList.sort(function (a, b) {
        return a.process.id - b.process.id;
    });
    processTree.roots.sort(function (a, b) {
        return a.process.id - b.process.id;
    });
    for (var i = 0; i < processTree.roots.length; i++) {
        addChildren(processTree.roots[i], nodeList);
    }
    $('.collapsable').remove();
    $('.expandable').remove();
    for (var i = 0; i < processTree.roots.length; i++) {
        printTreeTable(processTree.roots[i], 0, $('#proctable'));
    }
}

function enableCollabsableNodes() {
    //http://stackoverflow.com/questions/5636375/how-to-create-a-collapsing-tree-table-in-html-css-js
    $('#proctable').on('click', '.toggle', function (e) {
        e.preventDefault();
        e.stopPropagation();

        //Gets all <tr>'s  of greater depth
        //below element in the table
        var findChildren = function (tr) {
            var depth = tr.data('depth');
            return tr.nextUntil($('tr').filter(function () {
                return $(this).data('depth') <= depth;
            }));
        };

        var el = $(this);
        var tr = el.closest('tr');
        var children = findChildren(tr);

        //Remove already collapsed nodes from children so that we don't
        //make them visible.
        var subnodes = children.filter('.expandable');
        subnodes.each(function () {
            var subnode = $(this);
            var subnodeChildren = findChildren(subnode);
            children = children.not(subnodeChildren);
        });

        //Change icon and hide/show children
        if (tr.hasClass('collapsable')) {
            tr.removeClass('collapsable').addClass('expandable');
            children.hide();
        } else {
            tr.removeClass('expandable').addClass('collapsable');
            children.show();
        }
        return children;
    });
}

function downloadURL(url) {
    var hiddenIFrameID = 'hiddenDownloader', iframe;
    iframe = document.getElementById(hiddenIFrameID);
    if (iframe === null) {
        iframe = document.createElement('iframe');
        iframe.id = hiddenIFrameID;
        iframe.style.display = 'none';
        document.body.appendChild(iframe);
    }
    iframe.src = url;
}

function overrideRightClickMenu() {
    var options = {
        selector: 'tr',
        trigger: 'both',
        callback: function (key) {
            var process = $(this).data('proc');
            switch (key) {
                case 'kill':
                    $(this).removeClass('hoverable');
                    $(this).addClass('dying');
                    process.kill().done(function () {
                        return processExplorerSetupAsync();
                    }).fail(function () {
                        return processExplorerSetupAsync();
                    });
                    break;
                case 'dump1':
                    downloadURL(process.minidump + '?dumpType=1');
                    break;
                case 'dump2':
                    downloadURL(process.minidump + '?dumpType=2');
                    break;
                case 'gcdump':
                    downloadURL(process.gcdump);
                    processExplorerSetupAsync();
                    break;
                case 'properties':
                    process.Dialog().dialog('open');
                    $('li').blur();
                    break;
            }
        },
        items: {
            'kill': { name: 'Kill' },
            'dump': {
                name: 'Download Memory Dump',
                'items': {
                    'dump1': { name: 'Mini Dump' },
                    'dump2': { name: 'Full Dump' }
                }
            },
            'gcdump': { name: 'Download GC Dump' },
            'sep1': '---------',
            'properties': { name: 'Properties' }
        },
        events: {
            hide: function () {
                $(this).removeClass('selectedMenu');
            },
            show: function () {
                $(this).addClass('selectedMenu');
            }
        }
    };
    $('#proctable').contextMenu(options);
}

function arrayToDivs(lines) {
    var htmls = [];
    var tmpDiv = jQuery(document.createElement('div'));
    for (var i = 0; i < lines.length; i++) {
        htmls.push(tmpDiv.text(lines[i]).html());
    }
    return htmls.join('<br />');
}

function searchForHandle() {
    var name = $('#name').val().toLowerCase();
    var result = [];
    for (var i = 0; i < nodeList.length; i++) {
        for (var j = 0; j < nodeList[i].process.open_file_handles.length; j++) {
            var check = nodeList[i].process.open_file_handles[j].replace(/\\+$/, '').toLowerCase();
            check = check.substring(check.lastIndexOf('\\'));
            if (check.indexOf(name) !== -1) {
                result.push(nodeList[i].process.FullName + ':' + nodeList[i].process.id + ' -> ' + nodeList[i].process.open_file_handles[j]);
            }
        }
    }
    if (result.length > 0) {
        $('#handle-result').html(arrayToDivs(result));
    } else {
        $('#handle-result').html(ErrorDiv('No handle found').outerHTML);
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

    $('#find-file-handle').button().click(function () {
        $('#dialog-form').dialog('open');
    });

    $('#dialog-form').dialog({
        autoOpen: false,
        height: 300,
        width: 300,
        buttons: {
            'Search': function () {
                return searchForHandle();
            },
            Cancel: function () {
                $(this).dialog('close');
            }
        }
    });

    $('#dialog-form').keypress(function (e) {
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
