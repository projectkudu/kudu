(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.siteManager = function (options) {
        var createDevelopmentSiteUrl = options.createDevelopmentSiteUrl,
            setWebRootUrl = options.setWebRootUrl,
            that = null;

        that = {
            createDevelopmentSite: function () {
                return $.post(createDevelopmentSiteUrl, {}, 'json');
            },
            setWebRoot: function (path) {
                return $.post(setWebRootUrl, { projectPath: path }, 'json');
            }
        };

        return that;
    }

})(jQuery);
