(function ($, window) {
    "use strict";

    if (typeof (window.signalR) === "function") {
        return;
    }

    var signalR = function (url) {
        /// <summary>Creates a new signalR connection for the given url</summary>
        /// <param name="url" type="String">The URL of the long polling endpoint</param>
        /// <returns type="signalR" />

        return new signalR.fn.init(url);
    };
    signalR.fn = signalR.prototype = {
        init: function (url) {
            this.url = url;
        },

        xhr: null,

        send: function (data, callback) {
            /// <summary>Sends data over this connection</summary>
            /// <param name="data" type="String">The data to send</param>
            /// <param name="callback" type="Function">A callback to be invoked when the send has completed</param>
            $.ajax(this.url, {
                type: "POST",
                data: data,
                dataType: "json",
                success: callback,
                error: function (data, textStatus) {
                    if (textStatus === "abort") {
                        return;
                    }
                    $(instance).trigger("onError", [data]);
                }
            });
            return this;
        },

        sending: function (callback) {
            /// <summary>Adds a callback that will be invoked before anything is sent over the connection</summary>
            /// <returns type="signalR" />
            var that = this;
            $(that).bind("onSending", function (e, data) {
                callback.call(that);
            });
            return that;
        },

        received: function (callback) {
            /// <summary>Adds a callback that will be invoked after anything is received over the connection</summary>
            /// <returns type="signalR" />
            var that = this;
            $(that).bind("onReceived", function (e, data) {
                callback.call(that, data);
            });
            return that;
        },

        error: function (callback) {
            /// <summary>Adds a callback that will be invoked after an error occurs with the connection</summary>
            /// <returns type="signalR" />
            var that = this;
            $(that).bind("onError", function (e, data) {
                callback.call(that);
            });
            return that;
        },

        start: function (callback) {
            /// <summary>Starts listening</summary>
            var that = this;

            if (typeof (callback) === "function") {
                $(that).bind("onStart", function (e, data) {
                    callback.call(that);
                });
                return;
            }

            $(that).trigger("onStart");
            if (that.xhr) {
                that.stop();
            }
            window.setTimeout(function () {
                (function poll(instance) {
                    $(instance).trigger("onSending");
                    instance.xhr = $.ajax(instance.url, {
                        type: "POST",
                        data: instance.data,
                        dataType: "json",
                        success: function (data) {
                            $(instance).trigger("onReceived", [data]);
                            poll(instance);
                        },
                        error: function (data, textStatus) {
                            if (textStatus == "abort") {
                                return;
                            }

                            $(instance).trigger("onError", [data]);
                            window.setTimeout(function () {
                                poll(instance);
                            }, 2 * 1000);
                        }
                    })
                })(that);
            }, 250); // Have to delay initial poll so Chrome doesn't show loader spinner in tab
            return that;
        },

        stop: function () {
            /// <summary>Stops listening</summary>
            if (this.xhr) {
                this.xhr.abort();
            }
            return this;
        }
    };

    signalR.fn.init.prototype = signalR.fn;

    window.signalR = signalR;

})(window.jQuery, window);