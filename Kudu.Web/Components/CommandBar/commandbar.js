(function ($) {
    $.commandBar = function (options) {
        var config = {
        },
        $this = $('<div/>').addClass('collapsed'),
        $header = $('<div/>').addClass('header'),
        $ghost = $('<div/>').addClass('ghost')
                            .addClass('commandBar'),
        $tabs = $('<ul />').addClass('tabs'),
        $body = $('<div/>').addClass('body'),
        $toggle = $('<a/>').addClass('toggle')
                           .attr('href', '#')
                           .addClass('icon-notext')
                           .addClass('icon-expand-bar')
                           .html('&nbsp'),
        $window = $(window),
        that = null,
        sections = {};

        $.extend(config, options);

        $this.addClass('commandBar');

        // Move this to the bottom of the screen like firebug
        // ensure the ghost has always the same size as the container
        // and the container is always positioned correctly        
        $toggle.appendTo($header);
        $tabs.appendTo($header);
        $header.appendTo($this);
        $body.appendTo($this);
        $ghost.appendTo(document.body);
        $this.appendTo(document.body);

        function setActiveSection(section) {
            $this.find('.active').removeClass('active');
            $this.find('[data-section="' + section + '"]').addClass('active');

            $(that).trigger('commandBar.sectionChanged', [section]);
        }


        $this.delegate('.tab', 'click', function () {
            var section = $(this).data('section');
            setActiveSection(section);
        });

        $toggle.toggle(
        function () {
            $this.toggleClass('collapsed');
            $(this).removeClass('icon-expand-bar');
            $(this).addClass('icon-collapse-bar');
            $tabs.show();

            $window.resize();

            $(that).trigger('commandBar.collapsed');
        },
        function () {
            $this.toggleClass('collapsed');
            $(this).removeClass('icon-collapse-bar');
            $(this).addClass('icon-expand-bar');
            $tabs.hide();

            $window.resize();

            $(that).trigger('commandBar.expanded');
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
        $(window).resize(syncResize);
        $(window).resize();

        that = {
            addSection: function (name, $container) {
                var $header = $('<li />').addClass('tab')
                                         .attr('data-section', name)
                                         .html(name);

                $header.appendTo($tabs);
                $container.height('100%');
                $container.appendTo($body);
                $container.attr('data-section', name)
                          .addClass('section');

                setActiveSection(name);
            }
        };

        return that;
    }
})(jQuery);