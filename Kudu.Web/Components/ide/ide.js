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
            $commitViewer = $('<div/>').addClass('working'),
            commitViewer = null,
            commandWindow = null,
            commandBar = null,
            tabManager = null,
            editor = null,
            fileExplorer = null,
            notificationBar = null,
            statusBar = null,
            currentHeight = null,
            refreshingWorkingDirectory = true,
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
            tab: $('#tabManager_tabTemplate'),
            diff: $('#diffViewer_files')
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
                        if (mode === modes.live) {
                            $devMode.hide();
                        }
                        else {
                            refreshingWorkingDirectory = true;
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
                var d = $.Deferred();
                var working = devenv.getWorking()
                      .done(function (working) {
                          if (working) {
                              alert('You have some pending changes');
                              commandBar.select('Working Directory');
                              commandBar.show();
                              d.rejectWith(core);
                              return;
                          }

                          // TODO: Check for pending changes in the repository
                          var token = notificationBar.show('Deploying your changes to the live site');
                          var loadingToken = statusBar.show('Deploying...');
                          return devenv.goLive()
                             .done(function () {
                                 statusBar.hide(loadingToken);

                                 notificationBar.hide(token).done(function () {
                                     var link = '<a href="' + siteUrl + '" target="_blank">' + siteUrl + '</a>';
                                     notificationBar.show('Your changes are live ' + link);
                                 });

                                 d.resolveWith(core);
                             });
                      });

                return d;
            },
            saveDocument: function (file) {
                var path = file.getRelativePath();
                var content = editor.getContent();

                var token = statusBar.show('Saving ' + path + '...');
                return devenv.saveFile({
                    path: path,
                    content: content
                })
                        .done(function () {
                            file.setDirty(false);
                        })
                    .always(function () {
                        statusBar.hide(token);
                    });
            },
            executeCommand: function (command) {
                return devenv.executeCommand(command);
            },
            build: function () {
                return devenv.build()
                      .done(function () {
                          commandBar.select('Console');
                          commandBar.show();
                      });
            },
            deleteDocument: function (file) {
                var path = file.getRelativePath();

                var token = statusBar.show('Deleting ' + path + '...');
                return devenv.deleteFile(path)
                             .done(function () {
                                 fs.removeFile(path);
                             })
                             .always(function () {
                                 statusBar.hide(token);
                             });
            },
            // Source control method
            updateWorkingChanges: function () {
                return devenv.getWorking()
                             .done(function (working) {
                                 if (working) {
                                     commitViewer.refresh(working);
                                 }
                                 else {
                                     commitViewer.clear();
                                 }
                             });
            },
            commitWorkingChanges: function (message) {
                // TODO: Pass selected files
                devenv.commit(message)
                .done(function (changeSet) {
                    if (changeSet) {
                        var token = notificationBar.show('Successfully commited ' + changeSet.ShortId);
                        commitViewer.clear();
                        window.setTimeout(function () {
                            notificationBar.hide(token);
                        }, 3000);
                    }
                    else {
                        var token = notificationBar.show('No pending changes');
                        window.setTimeout(function () {
                            notificationBar.hide(token);
                        }, 1000);
                    }
                });
            },
            revertFile: function (path) {
                var file = fs.getFile(path);
                var path = file ? file.getRelativePath() : path;
                devenv.revertFile(path)
                      .done(function () {
                          devenv.openFile(path)
                                .done(function (content) {
                                    var tab = tabManager.get(path);

                                    if (!file) {
                                        fs.addFile(path);
                                    }

                                    if (tab) {
                                        editor.setContent(path, content);
                                    }

                                    file.setBuffer(content);
                                })
                                .fail(function (e) {
                                });
                      });
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
        commitViewer = $commitViewer.commitViewer({
            templates: templates
        });

        commandWindow = $console.console();

        commandBar = $.commandBar({
            sections: {
                'Console': { container: $console, icon: 'icon-console' },
                'Working Directory': { container: $commitViewer, icon: 'icon-working' }
            }
        });

        commandBar.select('Console');

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

        $(commandBar).bind('commandBar.sectionChanged', function (e, section) {
            if (section == 'Working Directory') {
                if (refreshingWorkingDirectory === true) {
                    core.updateWorkingChanges()
                        .done(function () {
                            refreshingWorkingDirectory = false;
                        });
                }
            }
        });

        $(commandWindow).bind('console.runCommand', function (e, command) {
            if (command == 'cls') {
                commandWindow.clear();
                commandWindow.completeCommand();
            }
            else {
                core.executeCommand(command);
            }
        });

        $(commitViewer).bind('commitViewer.commit', function (e, message) {
            core.commitWorkingChanges(message);
        });

        $(commitViewer).bind('commitViewer.refresh', function (e, message) {
            core.updateWorkingChanges();
        });

        $(commitViewer).bind('commitViewer.openFile', function (e, path) {
            var file = fs.getFile(path);
            tabManager.setActive(file.getPath());
        });

        $(commitViewer).bind('commitViewer.beforeRevertFile', function (e) {
            if ($.dialogs.show('Are you sure you want to revert ' + e.path)) {
                core.revertFile(e.path);
            }
            else {
                e.preventDefault();
            }
        });

        $(commitViewer).bind('commitViewer.afterRevertFile', function (e, path) {

        });

        $(fileExplorer).bind('fileExplorer.fileOpened', function (e, file) {
            tabManager.setActive(file.getPath());
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
                e.preventDefault();

                // Make this non blocking
                var path = e.tab.file.getRelativePath();
                if ($.dialogs.show("Do you want to save the changes to " + path)) {
                    core.saveDocument(e.tab.file);
                    tabManager.remove(e.tab.file.getPath());
                }
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

        var performSave = $.utils.throttle(function () {
            var tab = tabManager.getActive();
            if (tab) {
                core.saveDocument(tab.file);
            }
        }, 50);

        $(document).bind('keydown', 'ctrl+s', function (ev) {
            performSave();
            ev.preventDefault();
            return false;
        });

        $(document).bind('keydown', 'del', $.utils.throttle(function (ev) {
            if (fileExplorer.hasFocus()) {
                var selectedNode = fileExplorer.selectedNode();
                // TODO: Prompt here
                if (selectedNode) {
                    var path = selectedNode.item().getRelativePath();
                    if ($.dialogs.show('Are you sure you want to delete ' + path + '?')) {
                        if (selectedNode.isFile()) {
                            core.deleteDocument(selectedNode.item());
                        }
                        else {
                            fs.removeDirectory(selectedNode.path);
                        }
                    }

                    ev.preventDefault();
                    return false;
                }
            }
        }, 50));

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

        $(window).resize(function () {
            adjustHeight();
        });


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
                core.build();
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
