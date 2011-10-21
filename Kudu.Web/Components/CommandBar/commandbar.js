(function ($) {
    $.commandBar = function (options) {
        var config = {
        },
        $this = $('<div/>').addClass('collapsed'),
        $header = $('<div/>').addClass('header'),
        $ghost = $('<div/>').addClass('ghost')
                            .addClass('commandBar'),
        $resizeHandle = $('<div/>').addClass('resize'),
        $tabs = $('<ul />').addClass('tabs'),
        $body = $('<div/>').addClass('body'),
        $toggle = $('<a/>').addClass('toggle')
                           .attr('href', '#')
                           .addClass('icon-notext')
                           .addClass('icon-expand-bar')
                           .html('&nbsp'),
        $window = $(window),
        that = null,
        sections = options.sections;

        $.extend(config, options);

        $this.addClass('commandBar');
        $resizeHandle.addClass('ui-resizable-handle');
        $resizeHandle.addClass('ui-resizable-n');

        // Move this to the bottom of the screen like firebug
        // ensure the ghost has always the same size as the container
        // and the container is always positioned correctly        
        $toggle.appendTo($header);
        $tabs.appendTo($header);
        $resizeHandle.appendTo($this);
        $header.appendTo($this);
        $body.appendTo($this);
        $ghost.appendTo(document.body);
        $this.appendTo(document.body);

        $this.resizable({
            minHeight: 350,
            handles: { n: '.resize' },
            grid: 31,
            start: function () {
            },
            resize: function (ev) {
            },
            stop: function () {

            }
        });

        function setActiveSection(section) {
            $this.find('.active').removeClass('active');
            $this.find('[data-section="' + section + '"]').addClass('active');

            $(that).trigger('commandBar.sectionChanged', [section]);
        }

        function addSection(name, section) {
            var $header = $('<li />').addClass('tab')
                                         .attr('data-section', name)
                                         .html(name);

            $container = section.container;

            if (section.icon) {
                $header.addClass('icon');
                $header.addClass(section.icon);
            }

            $header.appendTo($tabs);
            $container.height('100%');
            $container.appendTo($body);
            $container.attr('data-section', name)
                          .addClass('section');
        }

        $.each(sections, function (key, value) {
            addSection(key, value);
            setActiveSection(key);
        });


        $this.delegate('.tab', 'click', function (ev) {
            var section = $(this).data('section');
            setActiveSection(section);

            ev.preventDefault();
            return false;
        });

        $toggle.click(function () {
            that.toggle();
        });

        var throttledToggle = $.utils.throttle(function () { that.toggle(); }, 50);

        $(document).bind('keydown', 'ctrl+k', function (ev) {
            throttledToggle();
            ev.preventDefault();
            return false;
        });

        var syncResize = function () {
            var containerHeight = $this.outerHeight(),
                headerHeight = $header.outerHeight(),
                windowHeight = $window.height(),
                scrollTop = $window.scrollTop();

            var top = windowHeight - containerHeight + scrollTop;
            var bodyHeight = containerHeight - headerHeight;

            $body.height(bodyHeight);
            $ghost.height(containerHeight);
            $this.offset({ top: top, left: 0 });
            $this.width('100%');
        };

        // Ensure the size/position is correct whenver the container or the browser is resized
        $this.resize(syncResize);
        $window.resize(syncResize);
        $window.resize();

        that = {
            show: function () {
                $this.removeClass('collapsed');
                $toggle.removeClass('icon-expand-bar');
                $toggle.addClass('icon-collapse-bar');
                $tabs.show();

                $window.resize();

                $(that).trigger('commandBar.expanded');
            },
            hide: function () {
                $this.addClass('collapsed');
                $toggle.removeClass('icon-collapse-bar');
                $toggle.addClass('icon-expand-bar');
                $tabs.hide();

                $window.resize();

                $(that).trigger('commandBar.collapsed');
            },
            toggle: function () {
                if ($this.hasClass('collapsed')) {
                    that.show();
                }
                else {
                    that.hide();
                }
            },
            select: setActiveSection,
            selected: function () {
                return $this.find('.active').data('section');
            }
        };

        return that;
    }
})(jQuery);