(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.icons = {
        '.cshtml': 'cshtml',
        '.ascx': 'ascx',
        '.aspx': 'aspx',
        '.cs': 'cs',
        '.config': 'config',
        '.css': 'css',
        '.dll': 'dll',
        '.master': 'master',
        '.js': 'js',
        '.php': 'php'
    };

    $.getIconClass = function (file) {
        return $.icons[file.getExtension()] || 'default'
    }

})(jQuery);