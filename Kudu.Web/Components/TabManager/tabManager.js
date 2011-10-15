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

            // Mark dirty state
            $.each($this.find('.tab'), function () {
                var path = $(this).data('path');
                var file = fs.getFile(path);
                if (file.isDirty()) {
                    $(this).find('.dirty').show();
                }
            });
        }

        function getTabElement(file) {
            return $('#tab-' + file.getElementId());
        }

        $this.addClass('tabManager');

        $this.delegate('.delete', 'click', function (ev) {
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
            var path = $(this).closest('.tab').data('path');
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
            refresh: renderTabs
        };

        return that;
    };

})(jQuery);