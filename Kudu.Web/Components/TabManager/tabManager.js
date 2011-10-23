(function ($) {
    $.fn.tabManager = function (options) {
        // Get the file system from the options
        var fs = options.fileSystem,
            templates = options.templates,
            $this = $(this),
            tabs = [],
            tabsLookup = {},
            that = null;

        function renderTabs() {
            var active = getActiveTab();

            var tabsClone = [];
            $.each(tabsLookup, function (key, tab) {
                tabsClone.push(tab);
                tab.icon = $.getIconClass(tab.file);
                tab.active = tab == active;
            });

            $this.html($.render(templates.tab, tabsClone));

            // TODO: Cache this
            var $tabs = $this.find('.tab'),
                $text = $tabs.find('.text'),
                $dirty = $tabs.find('.dirty'),
                $close = $tabs.find('.close');

            // Percentage of the tab that will take up text (file name)
            var textPercentage = 6;
            // Margin and padding for each tab
            var tabExtra = $tabs.outerWidth() - $tabs.width();
            // Calculate the total with we have to work with
            var width = $this.outerWidth() - ((tabExtra * tabs.length) + 1);
            // Calculate the width of each tab
            var tabWidth = width / tabs.length;

            // Get the sizes of the contents of the tab
            // The close icon width
            var closeTabIconSize = $close.outerWidth();

            // The width of the dirty icon
            var dirtySize = $dirty.outerWidth();

            // Calculate the with for parts of the tab that are not showing text
            var nonTextArea = closeTabIconSize + dirtySize;

            // The minimum size of a tab
            var minTabSize = 35;

            // The maximum size of a tab (TODO: Make it configurable)
            var maximumTabWidth = 200;

            // Make sure we're never over the maximum
            tabWidth = Math.min(maximumTabWidth, tabWidth);

            // Find the maximum percentage of visible text
            while (textPercentage > 0) {
                var textWidth = (textPercentage * tabWidth) / 10;
                var remaining = tabWidth - textWidth - tabExtra;
                // If the remaining space is enough for non-text part of the tab then we're ok
                if (remaining > nonTextArea) {
                    break;
                }
                // Otherwise reduce the % width
                textPercentage--;
            }

            if (tabWidth < minTabSize) {
                // We're too small to hide the text area
                $text.hide();
            }

            // Update the tabs based on ours calculations
            $.each($tabs, function () {
                var path = $(this).data('path');
                var file = fs.getFile(path);
                if (file.isDirty()) {
                    $(this).find('.dirty').show();
                }

                $(this).width(tabWidth);
                $(this).find('.text').width(textPercentage + '0%');
            });
        }

        function getTabElement(file) {
            return $this.find('[data-path="' + file.getPath() + '"]')
                        .filter('.tab');
        }

        $this.addClass('tabManager');

        $(fs).bind('fileSystem.removeFile', function (e, file) {
            removeTab(file.getPath());
        });

        $this.delegate('.close', 'click', function (ev) {
            var path = $(this).closest('.tab').data('path');
            var tab = tabsLookup[path];

            var event = $.Event('tabManager.beforeTabClosed', { tab: tab });
            $(that).trigger(event);

            if (!event.isDefaultPrevented()) {
                removeTab(path);
            }

            ev.preventDefault();
            return false;
        });

        $this.delegate('.open', 'click', function (ev) {
            var path = $(this).data('path');
            setActiveTab(path);

            var tab = tabsLookup[path];

            $(that).trigger('tabManager.tabClicked', [tab]);

            ev.preventDefault();
            return false;
        });

        function addTabItem(path) {
            var tab = tabsLookup[path];
            if (!tab) {
                var file = fs.getFile(path);
                tab = {
                    file: file,
                    _dirty: function (e, value) {
                        $tab = getTabElement(file);
                        if (value === true) {
                            $tab.find('.dirty').show();
                        }
                        else {
                            $tab.find('.dirty').hide();
                        }
                    }
                };

                // Bind to the file's dirty event
                $(file).bind('file.dirty', tab._dirty);

                tabs.push(tab);
                tab.index = tabs.length - 1;
                tabsLookup[path] = tab;
            }
        }

        function setActiveTab(path) {
            var prevTab = getActiveTab();
            $(that).trigger('tabManager.beforeActiveTabChanged', [prevTab]);

            addTabItem(path);

            var tab = tabsLookup[path];

            if (tab) {
                moveToFront(tab);
            }

            renderTabs();

            $(that).trigger('tabManager.afterActiveTabChanged', [tab]);
        }

        function removeTabItem(tab) {
            var index = $.inArray(tab, tabs);
            if (index != -1) {
                tabs.splice(index, 1);
                $.each(tabs, function () {
                    this.index--;
                });
            }
        }

        function removeTab(path) {
            var tab = tabsLookup[path];

            if (!tab) {
                return;
            }

            // Remove the handler
            $(tab.file).unbind('file.dirty', tab._dirty);

            removeTabItem(tab);

            delete tabsLookup[path];

            renderTabs();

            $(that).trigger('tabManager.afterTabClosed', [tab]);
        }

        function getActiveTab() {
            if (tabs.length) {
                return tabs[tabs.length - 1];
            }
            return null;
        }

        function moveToFront(tab) {
            if (tab.index !== (tabs.length - 1)) {
                removeTabItem(tab);
                tabs.push(tab);
                tab.index = tabs.length - 1;
            }
        }

        that = {
            add: function (path) {
                addTabItem(path);

                renderTabs();
            },
            setActive: setActiveTab,
            get: function (path) {
                return tabsLookup[path];
            },
            remove: removeTab,
            getActive: getActiveTab,
            nextTab: function () {
                if (tabs.length > 1) {
                    moveToFront(tabs[0]);
                }
            },
            refresh: renderTabs,
            getTabFiles: function () {
                return $.map(tabs, function (tab) { return tab.file; });
            },
            closeAll: function () {
                for (var key in tabsLookup) {
                    delete tabsLookup[key];
                }

                for (var i = 0; i < tabs.length; ++i) {
                    $(that).trigger('tabManager.afterTabClosed', [tabs[i]]);
                }

                tabs.length = 0;

                renderTabs();
            }
        };

        return that;
    };

})(jQuery);