/// <reference path="Scripts/jquery-1.6.1.js" />
/// <reference path="loader.js" />
/// <reference path="../Scripts/sammy-latest.min.js" />
/// <reference path="../Scripts/jquery.cookie.js" />

(function ($, window) {
    $(function () {
        var deployment = $.connection.deployment;

        function getLogClass(type) {
            switch (type) {
                case 0:
                    return "icon-log-message";
                case 1:
                    return "icon-log-warning";
                case 2:
                    return "icon-log-error";
            }
        }

        window.getLogClass = getLogClass;

        function onError(e) {
            $('#error').html(e);
            $('#error').show();
        }

        function updateStatus(result) {
            if (deployment.id === result.Id) {
                return;
            }

            var oldItem = $('#' + deployment.id);
            var newItem = $('#' + result.Id);

            newItem.find('.loading').show();
            oldItem.find('.status').hide();

            // Update the deployment status
            var status = newItem.find('.deploy-status');
            var statusText = newItem.find('.status-text');
            statusText.html(result.StatusText ? '(' + result.StatusText + ')' : '');
            status.html(result.Status);
            status.show();

            if (result.Status == 'Success') {
                newItem.find('.loading').hide();
                newItem.find('.deploy').hide();
                newItem.find('.status').show();

                oldItem.find('.deploy').show();

                deployment.id = result.Id;
            }
            else if (result.Status == 'Failed') {
                oldItem.find('.loading').hide();
                oldItem.find('.deploy').show();
                oldItem.find('.status').show();

                newItem.find('.failed').show();
                newItem.find('.loading').hide();
            }
        }

        function initialize() {
            $('#url').click(function () {
                $(this)[0].select();
            });

            $('#deployments').delegate('.update', 'click', function () {
                var newId = $(this).attr('data-id');
                var branch = $(this).attr('data-branch');

                deployment.deploy(branch || newId);

                return false;
            });
        }

        function loadDeployments(onComplete) {
            loadingRepository = true;
            $('#deploy-log').hide();

            $('#deployments').html('');
            $('#log').show();

            var token = window.loader.show('Loading deployments...');
            deployment.getDeployments()
           .done(function (deployments) {
               $.each(deployments, function () {
                   this.showDeploy = !this.Active && this.Status == 'Success';

                   this.showLoading =
                                   this.Status !== 'Success' &&
                                   this.Status !== 'Failed';

                   this.failed = this.Status === 'Failed';
               });

               $('#deployments').append($('#deployment').render(deployments));
           })
           .fail(onError)
           .always(function () {
               window.loader.hide(token);
               loadingRepository = false;
           });
        }

        function viewDeployLog(id) {
            $('#log').hide();

            var token = window.loader.show('Loading deployment log...');

            deployment.getDeployLog(id, function (logs) {
                $('#deploy-log').html($('#logTemplate').render(logs));
                $('#deploy-log').show();
            })
            .fail(onError)
            .always(function () {
                window.loader.hide(token);
            });
        }

        var app = $.sammy(function () {
            this.get('#/', function () {
                loadDeployments();
                return false;
            });

            this.post('#/', function () {
                this.redirect('#/');

                return false;
            });

            this.get('#/view-log/:id', function () {
                viewDeployLog(this.params.id);
            });
            return false;
        });

        initialize();

        var lastResult;

        deployment.updateDeployStatus = function (result) {
            // Don't force the view to change if it's not visible
            if (!$('#log').is(':visible')) {
                return;
            }

            if (loadingRepository === false) {
                if (!document.getElementById(result.Id)) {
                    loadDeployments(function () {
                        if (lastResult) {
                            updateStatus(lastResult);
                            lastResult = null;
                        }
                    });
                }
                else {
                    updateStatus(result);
                }
            }
            else {
                lastResult = result;
            }
        };

        $.connection.hub.start({ transport: "longPolling" }, function () {
            app.run('#/');
        });
    });

})(jQuery, window);