/// <reference path="jquery-1.5.2.js" />

(function (window, $) {
    window.skyDE = {
        fly: function (settings) {
            var config = {
                textArea: '#editor'
            };

            $.extend(config, settings);

            var editor = CodeMirror.fromTextArea($(config.textArea)[0], {
                lineNumbers: true,
                matchBrackets: true,
                indentUnit: 4,
                indentWithTabs: false,
                enterMode: "keep",
                tabMode: "shift",
                readOnly: true,
                onChange: function (editor) {
                    if (updatingEditor === false) {
                        onDocumentChange();
                    }
                }
            });

            var documentTabs = (function () {
                // ordered tabs
                var tabs = [];

                // for fast lookup
                var tabsLookup = {};

                function removeTab(tab) {
                    var index = $.inArray(tab, tabs);
                    if (index != -1) {
                        tabs.splice(index, 1);
                        $.each(tabs, function () {
                            this.index--;
                        });
                    }
                }

                function moveToFront(tab) {
                    if (tab.index !== (tabs.length - 1)) {
                        removeTab(tab);
                        tabs.push(tab);
                        tab.index = tabs.length - 1;
                    }
                }

                return {
                    add: function (path) {
                        var tab = tabsLookup[path];
                        if (!tab) {
                            tab = (function (path) {
                                return {
                                    getFile: function () {
                                        return fileSystem.getFile(path);
                                    }
                                }
                            })(path);

                            tabs.push(tab);
                            tab.index = tabs.length - 1;
                            tabsLookup[path] = tab;
                        }
                    },
                    setActive: function (path) {
                        var tab = tabsLookup[path];

                        if (tab) {
                            moveToFront(tab);
                        }
                    },
                    get: function (path) {
                        return tabsLookup[path];
                    },
                    remove: function (path) {
                        removeTab(tabsLookup[path]);
                        delete tabsLookup[path];
                    },
                    getActive: function () {
                        if (tabs.length) {
                            return tabs[tabs.length - 1];
                        }
                        return null;
                    },
                    nextTab: function () {
                        if (tabs.length > 1) {
                            moveToFront(tabs[0]);
                        }
                    },
                    getAll: function () {
                        var tabsClone = [];
                        $.each(tabsLookup, function () {
                            tabsClone.push(this);
                        });
                        return tabsClone;
                    }
                };
            })();


            var loader = window.loader;

            editor.setValue('');

            var documents = $.connection.documents;

            var fileSystem = new FileSystem(),
                                 iconMap = {},
                                 updatingEditor = false;


            iconMap['.cshtml'] = 'cshtml';
            iconMap['.ascx'] = 'ascx';
            iconMap['.aspx'] = 'aspx';
            iconMap['.cs'] = 'cs';
            iconMap['.config'] = 'config';
            iconMap['.tt'] = 'tt';
            iconMap['.css'] = 'css';
            iconMap['.js'] = 'js';
            iconMap['.dll'] = 'dll';
            iconMap['.master'] = 'master';
            iconMap['.php'] = 'php';

            function getMode(extension) {
                if (extension == '.css') {
                    return 'css';
                }
                if (extension == '.js') {
                    return 'javascript';
                }

                if (extension == '.html' ||
                    extension == '.htm' ||
                    extension == '.aspx' ||
                    extension == '.ascx' ||
                    extension == '.master') {
                    return 'htmlmixed';
                }

                if (extension == '.cshtml') {
                    return 'razor';
                }

                if (extension == '.php') {
                    return 'php';
                }

                if (extension == '.xml' || extension == '.config' || extension == '.nuspec') {
                    return 'xml';
                }
                if (extension == '.cs') {
                    return 'text/x-java';
                }
                return '';
            }

            $(document).bind('keydown', 'ctrl+s', function (evt) {
                saveDocument();
                evt.stopPropagation();
                evt.preventDefault();
                return false;
            });

            function openDocument(path, suppressLoading) {
                // If document is active save the content locally
                var activeDocument = getActiveDocument();
                if (activeDocument) {
                    activeDocument.setBuffer(editor.getValue());
                }

                var file = fileSystem.getFile(path);

                // If this file is dirty then just reopen it with the local changes
                if ((file.isDirty() === true || documentTabs.get(path)) && file.getBuffer() !== null) {
                    setContent(file, file.getBuffer());
                }
                else {
                    var token = null;
                    if (!suppressLoading) {
                        token = loader.show('Opening ' + file.getName() + '...');
                    }

                    var operation = documents.openFile(file.getRelativePath())
                                            .done(function (content) {
                                                setContent(file, content);
                                            })
                                            .fail(onError);

                    if (!suppressLoading) {
                        operation.always(function () {
                            loader.hide(token);
                        });
                    }

                    return operation;
                }
            }

            function setContent(file, content) {
                var mode = getMode(file.getExtension());
                updatingEditor = true;

                editor.setOption('mode', mode);
                editor.setOption('readOnly', false);
                editor.setValue(content);

                updatingEditor = false;

                var path = file.getPath();

                documents.activeDocument = path;
                documentTabs.add(path);
                documentTabs.setActive(path);
                file.setBuffer(content);

                refreshTabs();
            }

            function getActiveDocument() {
                if (documents && documents.activeDocument) {
                    return fileSystem.getFile(documents.activeDocument);
                }
                return null;
            }

            function getNewDocument(dir) {
                var fileCount = 0;

                do {
                    var targetFile = null;
                    if (fileCount) {
                        targetFile = dir + 'New File' + fileCount;
                    }
                    else {
                        targetFile = dir + 'New File';
                    }

                    if (!fileSystem.fileExists(targetFile)) {
                        break;
                    }

                    fileCount++;
                } while (fileSystem.fileExists(targetFile));

                return prompt('Enter the file name', targetFile.substr(dir.length));
            }

            function saveDocument() {
                var document = getActiveDocument();

                if (document) {
                    var path = document.getRelativePath();
                    var token = loader.show('Saving ' + path + '...');

                    documents.saveFile({
                        path: path,
                        content: editor.getValue()
                    })
                    .done(function () {
                        document.setDirty(false);
                        refreshTabs();
                    })
                    .fail(onError)
                    .always(function () {
                        loader.hide(token);
                    });
                }
            }

            function refreshTabs() {
                var tabs = documentTabs.getAll();
                var active = documentTabs.getActive();

                $.each(tabs, function () {
                    this.file = this.getFile();
                    this.css = iconMap[this.file.getExtension()] || 'default';
                    this.active = this == active;
                });

                $('#tabs').html($('#tabTemplate').render(tabs));

                if (!documents.activeDocument && active) {
                    openDocument(active.getFile().getPath());
                }
            }

            function updateFiles() {
                var token = loader.show('Updating project...');
                return documents.getStatus()
                         .done(function (project) {
                             refresh(project);
                             loader.hide(token);
                         })
                         .fail(function (e) {
                             onError(e);
                             loader.hide(token);
                         });
            }

            function collapseFolders() {
                $.each($('#file-browser').find('.icon-folder'), function () {
                    $(this).addClass('folder-collapsed');
                    $(this).siblings('.folder-contents').hide();
                });
            }

            function onDocumentChange() {
                var activeDocument = getActiveDocument();

                if (activeDocument) {
                    activeDocument.setDirty(true);
                    refreshTabs();
                }
            }

            function onError(e) {
                alert(e);
            }

            function initilize() {
                var browser = $('#file-browser');

                browser.delegate('.open', 'click', function () {
                    var path = $(this).closest('.file').attr('data-path');
                    openDocument(path);

                    $('.menu-contents').hide();
                    return false;
                });

                browser.delegate('.delete', 'click', function () {
                    var path = $(this).closest('.file').attr('data-path');
                    var file = fileSystem.getFile(path);

                    if (confirm('Are you sure you want to delete "' + file.getName() + '"')) {
                        var token = loader.show('Deleting ' + file.getName() + '...');
                        documents.deleteFile(file.getRelativePath())
                                 .done(function () {
                                     updateFiles().done(function () {
                                         closeTab(path);
                                     })
                                     .always(function () {
                                         loader.hide(token);
                                     });
                                 })
                                 .fail(function (e) {
                                     onError(e);
                                     loader.hide(token);
                                 });
                    }
                    return false;
                });

                browser.delegate('.new-file', 'click', function () {
                    var path = $(this).closest('.folder').attr('data-path');
                    var directory = fileSystem.getDirectory(path);
                    var relativePath = directory.getRelativePath();
                    var name = getNewDocument(relativePath);

                    if (name) {
                        // Expand the folder where the new file was added
                        $(this).parents('.menu').siblings('.icon-folder').removeClass('folder-collapsed');
                        $(this).parents('.menu').siblings('.folder-contents').show();

                        var fullPath = relativePath + name;

                        var token = loader.show('Creating file ' + fullPath + '...');
                        documents.saveFile({
                            path: fullPath,
                            content: ""
                        })
                        .done(function () {
                            updateFiles().done(function () {
                                var file = fileSystem.getFile(fullPath);
                                var operation = openDocument(file.getPath(), true);
                                if (operation) {
                                    operation.always(function () {
                                        loader.hide(token);
                                    });
                                }
                                else {
                                    loader.hide(token);
                                }
                            });
                        })
                        .fail(function (e) {
                            onError(e);
                            loader.hide(token);
                        });

                        $('.menu-contents').hide();
                    }
                    return false;
                });

                browser.delegate('.new-folder', 'click', function () {
                    var path = $(this).closest('.folder').attr('data-path');
                    var directory = fileSystem.getDirectory(path);
                    var relativePath = directory.getRelativePath();
                    var name = prompt('Enter folder name', 'New Folder');
                    if (name) {
                        // Expand the folder where the new file was added
                        $(this).parents('.menu').siblings('.icon-folder').removeClass('folder-collapsed');
                        $(this).parents('.menu').siblings('.folder-contents').show();

                        var fullPath = relativePath + name + '/';

                        documents.saveFile({
                            path: fullPath,
                            content: ""
                        })
                        .done(updateFiles)
                        .fail(onError);

                        $('.menu-contents').hide();
                    }
                    return false;
                });

                browser.delegate('.icon-folder', 'click', function () {
                    var path = $(this).closest('.folder').attr('data-path');
                    var directory = fileSystem.getDirectory(path);

                    if (!directory._isRoot()) {
                        $(this).toggleClass('folder-collapsed');
                        $(this).siblings('.folder-contents').toggle();
                        $('.menu-contents').hide();
                    }
                    return false;
                });

                browser.delegate('.menu > li', 'click', function (e) {
                    // Hide the other menus
                    var menuContents = $(this).find('.menu-contents');
                    browser.find('.menu > li').find('.menu-contents').not(menuContents).hide()
                    menuContents.toggle();
                    e.stopPropagation();
                    e.preventDefault();
                    return false;
                });

                var tabs = $('#tabs');

                tabs.delegate('.open', 'click', function () {
                    var path = $(this).closest('.file').attr('data-path');
                    var file = getActiveDocument();
                    if (file.getPath() != path) {
                        openDocument(path);
                    }

                    return false;
                });

                tabs.delegate('.delete', 'click', function () {
                    var path = $(this).closest('.file').attr('data-path');

                    var document = documentTabs.get(path);
                    if (document.getFile().isDirty() === true) {
                        // TODO: We really need Yes, No, Cancel here
                        if (!confirm('Do you want to save the changes to "' + document.getFile().getName() + '"?')) {
                            return;
                        }
                        else {
                            var token = loader.show('Saving ' + path + '...');
                            var activeDoc = documentTabs.getActive();
                            var content = activeDoc == document ? editor.getValue() : document.getFile().getBuffer();

                            documents.saveFile({
                                path: document.getFile().getRelativePath(),
                                content: content
                            })
                            .done(function () {
                                document.getFile().setDirty(false);
                                closeTab(path);
                            })
                            .fail(onError)
                            .always(function () {
                                loader.hide(token);
                            });
                        }
                    }
                    else {
                        closeTab(path);
                    }

                    return false;
                });
            }

            function closeTab(path) {
                if (documents.activeDocument == path) {
                    documents.activeDocument = null;
                    editor.setValue('');
                    editor.setOption('readOnly', true);
                }

                documentTabs.remove(path);

                refreshTabs();
            }

            function refresh(project) {
                var oldFiles = fileSystem.getFiles();

                fileSystem.create(project.Files);
                fileSystem.setReadOnly(project.IsReadOnly);
                fileSystem.setRootName(project.Name || '~/');

                var browser = $('#file-browser');

                // Store the state of each of the folders so we preserve them after the bind
                var folderCollapsedState = {};
                $.each(browser.find('.icon-folder'), function () {
                    var path = $(this).closest('.folder').attr('data-path');
                    folderCollapsedState[path] = $(this).hasClass('folder-collapsed');
                });

                var root = fileSystem.getRoot();

                browser.html($('#folderTemplate').render(root));

                // Setup images for file types
                $.each(browser.find('.open'), function () {
                    var path = $(this).closest('.file').attr('data-path');
                    var file = fileSystem.getFile(path);
                    var extension = file.getExtension();
                    var iconMapping = iconMap[extension] || 'default';

                    $(this).addClass('icon-' + iconMapping).addClass('icon');
                });

                // Preserve folder collapsed state
                $.each(browser.find('.icon-folder'), function () {
                    var path = $(this).closest('.folder').attr('data-path');
                    var directory = fileSystem.getDirectory(path);

                    if (folderCollapsedState[path] || directory.isEmpty()) {
                        $(this).addClass('folder-collapsed');
                        $(this).siblings('.folder-contents').hide();
                    }
                });

                // REVIEW: Temporary hack until we have a databinding framework or until we send back diffs from the server
                $.each(oldFiles, function () {
                    var newFile = fileSystem.getFile(this.getPath());
                    if (newFile) {
                        // Preserve the dirty and buffer
                        newFile.setDirty(this.isDirty());
                        newFile.setBuffer(this.getBuffer());
                    }
                });
            }

            initilize();

            $.connection.hub.start({ transport: "longPolling" }, function () {
                updateFiles().done(collapseFolders);
            });

            $(window.document).click(function () {
                $('.menu-contents').hide();
            });

            var commandLine = $.connection.commandLine;
            commandLine.appName = documents.appName;

            var cs = $('#console');
            var csGhost = $('#console-ghost');
            var cmd = $('#console-command');
            var consoleWindow = cs.find('.output');
            var buffer = consoleWindow.find('.buffer');
            var messages = consoleWindow.find('.messages');
            var toolBarHeight = $('#console .header').outerHeight();
            var executingCommand = false;

            // TODO: Refactor into something more general that tabs can use
            var commandStack = (function () {
                var at = 0;
                var stack = [];
                return {
                    next: function () {
                        if (at < stack.length) {
                            at++;
                        }
                    },
                    prev: function () {
                        if (at > 0) {
                            at--;
                        }
                    },
                    add: function (command) {
                        stack.push(command);
                        at = stack.length;
                    },
                    getValue: function () {
                        return stack[at];
                    }
                };
            })();

            $('#show-console').toggle(function () {
                cs.toggleClass('collapsed');
                $(this).removeClass('icon-expand');
                $(this).addClass('icon-collapse');
                cmd.focus();
                $(window).resize();
            },
            function () {
                cs.toggleClass('collapsed');
                $(this).removeClass('icon-collapse');
                $(this).addClass('icon-expand');
                $(window).resize();
            });

            function escapeHTMLEncode(str) {
                var div = document.createElement('div');
                var text = document.createTextNode(str);
                div.appendChild(text);
                return div.innerHTML;
            }

            function throttle(fn, delay) {
                var canInvoke = true;
                var invokeDelay = function () {
                    canInvoke = true;
                };

                return function () {
                    if (canInvoke) {
                        fn.apply(this, arguments);
                        canInvoke = false;
                        setTimeout(invokeDelay, delay);
                    }
                };
            }

            cmd.bind('keydown', 'esc', function (evt) {
                $(this).val('');
                // Clear the console
                evt.stopPropagation();
                evt.preventDefault();
                return false;
            });

            cmd.bind('keydown', 'tab', function (evt) {
                // Capture tab 
                evt.stopPropagation();
                evt.preventDefault();
                return false;
            });

            cmd.bind('keydown', 'ctrl+c', function (evt) {
                if (executingCommand) {
                    commandLine.cancel();
                    evt.stopPropagation();
                    evt.preventDefault();
                    return false;
                }
                return true;
            });

            cmd.bind('keydown', 'up', throttle(function (evt) {
                commandStack.prev();
                var command = commandStack.getValue();
                if (command && cmd.val() !== command) {
                    cmd.val(command);
                }
                evt.stopPropagation();
                evt.preventDefault();
                return false;
            }, 150));

            cmd.bind('keydown', 'down', throttle(function (evt) {
                commandStack.next();
                var command = commandStack.getValue();
                if (command && cmd.val() !== command) {
                    cmd.val(command);
                }
                evt.stopPropagation();
                evt.preventDefault();
                return false;
            }, 150));

            var triggerConsole = throttle(function () {
                $('#show-console').trigger('click');
            }, 150);

            $(document).bind('keydown', 'ctrl+k', function (evt) {
                triggerConsole();
                evt.stopPropagation();
                evt.preventDefault();
                return false;
            });

            var onConsoleCommandComplete = function () {
                messages.scrollTop(buffer[0].scrollHeight);
                buffer.find('.icon-prompt-loading').hide();
                executingCommand = false;
            };

            commandLine.done = onConsoleCommandComplete;

            commandLine.onData = function (result) {
                var lines = escapeHTMLEncode(result).split('\n');
                $.each(lines, function () {
                    var line = this.replace(/\s/g, '&nbsp;');
                    if (!line) {
                        line = '&nbsp;';
                    }
                    buffer.append('<li>' + line + '</li>');
                });

                messages.scrollTop(buffer[0].scrollHeight);
            };

            $('#new-command').submit(function () {
                if (executingCommand) {
                    // REVIEW: Should we queue commands like a real console?
                    return false;
                }

                var command = cmd.val();
                if (command == 'cls') {
                    buffer.html('');
                }
                else {
                    buffer.append('<li><span class="prompt">$</span><span class="command">' + command + '</span><span class="icon icon-prompt-loading"></span><li>');
                    messages.scrollTop(buffer[0].scrollHeight);

                    if (command) {
                        executingCommand = true;
                        commandLine.run(command)
                                   .fail(function (e) {
                                       onConsoleCommandComplete();
                                       onError(e);
                                   });

                        commandStack.add(command);
                    }
                    else {
                        buffer.find('.icon-prompt-loading').hide();
                    }
                }

                cmd.val('');
                return false;
            });

            // Focus the text box on click
            consoleWindow.click(function () {
                cmd.focus();
            });
        }
    };

    $(function () {
        skyDE.fly();

        var currentHeight;

        function isiPad() {
            return navigator.userAgent.match(/iPad/i) != null
        }

        function resolveHeight() {
            if (isiPad()) {
                if (window.innerWidth == 320) {
                    return window.innerWidth;
                }
                else {
                    return window.innerHeight;
                }
            }

            return window.innerHeight || (screen.height - 150);
        }

        var minHeight = 400;

        function adjustHeight() {
            var height = resolveHeight();

            if (currentHeight == height) {
                return;
            }

            currentHeight = height;

            if (window.console && window.console.log) {
                console.log('Screen height is ' + height);
            }

            var adjusted = Math.max(height - 150, minHeight);


            if (window.console && window.console.log) {
                console.log('Adjusting ide height to ' + adjusted + 'px');
            }

            $('#code-surface').css('height', adjusted + 'px');
        }

        adjustHeight();

        if (isiPad()) {
            // Detect screen layout changes
            setInterval(function () {
                adjustHeight();
            }, 500);
        }


        // Move this to the bottom of the screen like firebug
        // ensure the ghost has always the same size as the container
        // and the container is always positionned correctly
        var cs = $('#console');
        var csGhost = $('#console-ghost');

        csGhost.appendTo(document.body);
        cs.appendTo(document.body);

        var syncResize = function () {
            var _window = $(window);
            var containerHeight = cs.outerHeight();
            var containerWidth = cs.outerWidth();
            csGhost.height(containerHeight);
            var messagesHeight = 0;

            var windowHeight = _window.height();
            var scrollTop = _window.scrollTop();

            cs.offset({ top: windowHeight - containerHeight + scrollTop + messagesHeight, left: 0 });
            cs.width('100%');
        };

        // ensure the size/position is correct whenver the container or the browser is resized
        cs.resize(syncResize);
        $(window).resize(syncResize);
        $(window).resize();
    });

})(window, jQuery);