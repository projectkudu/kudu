(function ($) {
    $.fn.commandBar = function (options) {
        var config = {
        },
        $this = $(this);

        $.extend(config, options);

        $this.addClass('commandBar');

        var $ghost = $this.find('.ghost'); //$('<div/>').addClass('ghost');


        // Move this to the bottom of the screen like firebug
        // ensure the ghost has always the same size as the container
        // and the container is always positioned correctly
        
        // var cs = $('#console');
        // var csGhost = $('#console-ghost');

        $this.appendTo(document.body);
        // cs.appendTo(document.body);

        var syncResize = function () {
            var $window = $(window);
            var containerHeight = $this.outerHeight();
            var containerWidth = $this.outerWidth();
            $ghost.height(containerHeight);
            
            var windowHeight = $window.height();
            var scrollTop = $window.scrollTop();

            $this.offset({ top: windowHeight - containerHeight + scrollTop, left: 0 });
            $this.width('100%');
        };

        // Ensure the size/position is correct whenver the container or the browser is resized
        $this.resize(syncResize);
        $(window).resize(syncResize);
        $(window).resize();

    }
})(jQuery);