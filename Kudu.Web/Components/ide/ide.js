(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.ide = function (options) {
        var fs = new FileSystem(),
            $fileExplorer = options.fileExplorer,
            $tabManager = options.tabManager,
            $editor = options.editor,
            $activeView = options.activeView,
            $launcher = options.launcher,
            $projectList = options.projectList,
            $notificationBar = options.notificationBar,
            $statusBar = options.statusBar,
            $console = $('<div/>').addClass('commandWindow'),
            commandWindow = null,
            commandBar = null,
            tabManager = null,
            editor = null,
            fileExplorer = null,
            notificationBar = null,
            statusBar = null,
            currentHeight = null,
            siteUrl = options.siteUrl,
            devenv = $.connection.developmentEnvironment,
            minHeight = 450,
            createDevelopmentSite = options.createDevelopmentSite,
            siteManager = options.siteManager;

        devenv.applicationName = options.applicationName;

        var templates = {
            folder: $('#fileExplorer_folderTemplate'),
            file: $('#fileExplorer_fileTemplate'),
            deferredFolder: $('#fileExplorer_deferredFolderTemplate'),
            tab: $('#tabManager_tabTemplate')
        };

        var modes = {
            live: 0,
            developer: 1
        };

        var core = {
            openDocument: function (file) {
                var path = file.getRelativePath();

                if (file.getBuffer() !== null) {
                    editor.setContent(path, file.getBuffer());
                    editor.focus();
                    fileExplorer.setFocus(false);
                }
                else {
                    var token = statusBar.show('Opening ' + path + '...');

                    // Get the file content from the server
                    devenv.openFile(path)
                      .done(function (content) {
                          editor.setContent(path, content);
                          editor.focus();
                          fileExplorer.setFocus(false);

                          file.setBuffer(content);
                      })
                      .always(function () {
                          statusBar.hide(token);
                      });
                }
            },
            refreshProject: function () {
                var token = statusBar.show('Loading project...');
                return devenv.getProject()
                 .done(function (project) {
                     fs.create(project.Files);

                     fileExplorer.refresh();
                 })
                 .always(function () {
                     statusBar.hide(token);
                 });
            },
            setMode: function (mode) {
                var $devMode = $('.dev-mode');

                // Set the new mode
                devenv.mode = mode;
                $activeView.val(mode.toString());

                // TODO: Check for dirty documents here and ask if we want to save them
                tabManager.closeAll();

                editor.clear();

                core.refreshProject()
                    .done(function (project) {
                        if (mode == 0) {
                            $devMode.hide();
                        }
                        else {
                            $devMode.show();

                            if (project.Projects.length === 0) {
                                // Hide build actions if there's no projects
                                $('[data-action="build"]').hide();
                                $('.project-selection').hide();
                            }
                            else {
                                $projectList.html('');
                                $.each(project.Projects, function () {
                                    var file = fs.getFile(this);
                                    var $option = $('<option/>').attr('value', file.getRelativePath())
                                                                .html(file.getName());
                                    $projectList.append($option);
                                });

                                siteManager.setWebRoot(project.Projects[0]);
                                $('.project-selection').show();
                            }
                        }
                    });
            },
            goLive: function () {
                // TODO: Check for pending changes in the repository
                var token = notificationBar.show('Deploying your changes to the live repository');
                var loadingToken = statusBar.show('Deploying...');
                devenv.goLive()
                      .done(function () {
                          statusBar.hide(loadingToken);

                          notificationBar.hide(token).done(function () {
                              var link = '<a href="' + siteUrl + '" target="_blank">' + siteUrl + '</a>';
                              notificationBar.show('Your changes are live ' + link);
                          });
                      });
            },
            saveActiveDocument: function () {
                var tab = tabManager.getActive();
                if (tab) {
                    var path = tab.file.getRelativePath();
                    var content = editor.getContent();

                    var token = statusBar.show('Saving ' + path + '...');
                    devenv.saveFile({
                        path: path,
                        content: content
                    })
                    .done(function () {
                        tab.file.setDirty(false);
                    })
                    .always(function () {
                        statusBar.hide(token);
                    });
                }
            },
            executeCommand: function (command) {
                devenv.executeCommand(command);
            }
        };

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

        function adjustHeight() {
            var height = resolveHeight();

            if (currentHeight == height) {
                return;
            }

            currentHeight = height;
            var adjusted = Math.max(height - 150, minHeight);
            $editor.parent().parent().css('height', adjusted + 'px');
        }

        // Create components
        commandWindow = $console.console();

        commandBar = $.commandBar({
            sections: {
                'Console': $console
            }
        });

        statusBar = $statusBar.loader();

        notificationBar = $notificationBar.notificationBar();

        fileExplorer = $fileExplorer.fileExplorer({
            templates: templates,
            fileSystem: fs
        });

        tabManager = $tabManager.tabManager({
            templates: templates,
            fileSystem: fs
        });

        editor = $editor.editor({
            fileSystem: fs
        });


        devenv.commandComplete = function () {
            commandWindow.completeCommand();
        };

        devenv.processCommand = function (data) {
            commandWindow.log(data);
        };

        $(commandWindow).bind('console.runCommand', function (e, command) {
            if (command == 'cls') {
                commandWindow.clear();
                commandWindow.completeCommand();
            }
            else {
                core.executeCommand(command);
            }
        });

        $(fileExplorer).bind('fileExplorer.fileOpened', function (e, file) {
            var path = file.getPath();
            tabManager.setActive(path);
        });

        $(tabManager).bind('tabManager.beforeActiveTabChanged', function (e, tab) {
            if (tab) {
                tab.file.setBuffer(editor.getContent());
            }
        });

        $(tabManager).bind('tabManager.afterActiveTabChanged', function (e, tab) {
            core.openDocument(tab.file);
        });

        $(tabManager).bind('tabManager.beforeTabClosed', function (e) {
            if (e.tab.file.isDirty()) {
                $.dialogs.prompt("Do you want to save the changes to '" + e.tab.file.getPath() + "'", ['yes', 'no', 'cancel'])
                e.preventDefault();
            }
        });

        $(tabManager).bind('tabManager.afterTabClosed', function (e, tab) {
            if (tab.active) {
                // If the closed tab was active, get the new active tab
                // and set the editor's content to the new active tab
                var newActiveTab = tabManager.getActive();
                if (newActiveTab) {
                    // Get document content
                    core.openDocument(newActiveTab.file);
                }
                else {
                    // Clear the editor
                    editor.clear();
                }
            }

            // Set the buffer the null since the file has been closed
            tab.file.setBuffer(null);
        });

        $(editor).bind('editor.contentChanged', function (e) {
            var tab = tabManager.getActive();

            if (tab) {
                if (!tab.file.isDirty()) {
                    tab.file.setDirty(true);
                }
            }
        });

        var performSave = $.utils.throttle(core.saveActiveDocument, 50);

        $(document).bind('keydown', 'ctrl+s', function (ev) {
            performSave();
            ev.preventDefault();
            return false;
        });

        $(document).bind('keydown', 'del', function (ev) {
            if (fileExplorer.hasFocus()) {
                var selectedNode = fileExplorer.selectedNode();
                // TODO: Prompt here
                if (selectedNode) {
                    if (selectedNode.isFile()) {
                        fs.removeFile(selectedNode.path);
                    }
                    else {
                        fs.removeDirectory(selectedNode.path);
                    }

                    ev.preventDefault();
                    return false;
                }
            }
        });

        $activeView.change(function () {
            var mode = parseInt($(this).val());
            core.setMode(mode);
        });

        $projectList.change(function () {
            var projectPath = $(this).val();
            siteManager.setWebRoot(projectPath);
        });

        $.connection.hub.start(function () {
            if (createDevelopmentSite === true) {
                var token = notificationBar.show('Creating development site...');
                var loadingToken = statusBar.show('Cloning repository...');

                siteManager.createDevelopmentSite()
                           .done(function (url) {
                               $launcher.attr('href', url);
                               $activeView.addClass('hide');

                               notificationBar.hide(token);
                               statusBar.hide(loadingToken);

                               createDevelopmentSite = false;

                               core.setMode(modes.developer);
                           });
            }
            else {
                core.setMode(options.mode || modes.live);
            }
        });

        // Adjust the ide height
        adjustHeight();

        if (isiPad()) {
            // Detect screen layout changes
            setInterval(function () {
                adjustHeight();
            }, 500);
        }


        $.fs = fs;
        $.fe = fileExplorer;

        var globalActions = {
            'new-file': function () {
                var node = fileExplorer.selectedNode() || fileExplorer.node('/');

                if (node.isFile()) {
                    node = node.parentNode();
                }

                var path = node.path.substr(1) + 'New File';
                fs.addFile(path);

                fileExplorer.select(path);
                node.expand();
            },

            'save-all': function () {
                var files = tabManager.getTabFiles();

                if (files.length === 0) {
                    return;
                }

                var transformed = $.map(files, function (file) {
                    return {
                        path: file.getRelativePath(),
                        content: file.getBuffer()
                    };
                });

                devenv.saveAllFiles(transformed)
                      .done(function () {
                          $.each(files, function () {
                              this.setDirty(false);
                          });
                      });
            },
            'refresh-project': core.refreshProject,
            'go-live': function () {
                core.goLive();
            },
            'build': function () {

            }
        };

        $('body').delegate('[data-action]', 'click', function (ev) {
            var action = $(this).data('action');

            var a = globalActions[action];

            if (a) {
                a();
                ev.preventDefault();
                return false;
            }
        });
    };

})(jQuery);
