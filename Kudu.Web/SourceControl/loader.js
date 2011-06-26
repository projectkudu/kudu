(function ($, window) {
    var loader = function (selector) {
        this.selector = selector;
        this.token = 0;
    };

    loader.prototype = {
        show: function (value) {
            var token = this.token++;
            $(this.selector).attr('data-token', token);
            $(this.selector).html(value);
            $(this.selector).attr('class', 'icon icon-loading');
            $(this.selector).show();
            return token;
        },
        hide: function (token) {
            window.setTimeout($.proxy(function () {
                if ($(this.selector).attr('data-token') == token) {
                    $(this.selector).fadeOut('slow');
                }
            }, this), 100);
        }
    };

    window.loader = new loader('#status');
    window.infititeLoader = new loader('#infinite-status');

})(jQuery, window);