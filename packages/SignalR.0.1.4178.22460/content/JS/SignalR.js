/// <reference path="../Scripts/jquery-1.6.1.js" />
(function ($, window) {

    var SignalR = function (url) {
        /// <param name="url" type="String">The URL of the long polling endpoint</param>
        /// <returns type="SignalR" />

        return new SignalR.fn.init(url);
    }
    SignalR.fn = SignalR.prototype = {
        init: function (url) {
            this.url = url;
        },

        xhr: null,

        sent: function (callback) {
            /// <summary>Adds a callback that will be invoked when the long polling request is sent</summary>
            $(this).bind("onSent", callback);
            return this;
        },

        received: function (callback) {
            /// <summary>Adds a callback that will be invoked when the long polling request is returned</summary>
            var x = 1;
            $(this).bind("onReceived", function (e, data) {
                callback(data);
            });
            return this;
        },

        start: function () {
            /// <summary>Starts listening</summary>
            //var that = this;
            (function poll(instance) {
                $(instance).trigger("onSent");
                instance.xhr = $.ajax(instance.url, {
                    type: "POST",
                    success: function (data) {
                        $(instance).trigger("onReceived", data);
                        poll(instance);
                    }
                })
            })(this);
            return this;
        },

        stop: function () {
            /// <summary>Stops listening</summary>
            this.xhr.abort();
            return this;
        }
    };

    SignalR.fn.init.prototype = SignalR.fn;

    window.SignalR = SignalR;

})(jQuery, window);