$(document).ready(function () {
    if (!$("#instances-li").length) {
        return;
    }
    $("#instances-li").hide();
    $.ajax({
        type: "GET",
        url: '/instance/all',
        success: function (response) {
            try {
                var obj = JSON.parse(response);
                var ul = document.getElementById("instances_tab_options");
                if (obj.length > 1) {
                    for (var i = 0; i < obj.length; i++) {
                        var instanceTabBtn = document.createElement('li'); // is a node
                        instanceTabBtn.innerHTML = '<a href=\"#" onclick="NavigateToInstance(\'' + obj[i] + '\')">Instance: ' + obj[i].substring(0, 4) + '</a>';
                        instanceTabBtn.setAttribute("id", "inst-id-btn-" + obj[i]);
                        if (obj[i].trim().valueOf() === $.currInst) {
                            $("#instance-drop-down-text").text("Instance: " + obj[i].substring(0, 4) + "  ");
                        }
                        ul.appendChild(instanceTabBtn);
                    }
                    $("#instances-li").show();
                }
            }
            catch (err) {
                console.log(err);
            }
        }
    });
});

function NavigateToInstance(instId) {
    try {
        if ($.currInst !== null && $.currInst === instId) {
            return;
        }
        // ping the root with new instance to update ARRAffinity 
        $.ajax({
            url: '/?instance=' + instId,
            type: 'GET',
            success: function (data) {
                console.log('success ajax');
                $(".instances_tab_options_cls li.active").removeClass("active"); // reset all <li>to no active class
                $('#inst-id-btn-' + instId).addClass("active"); // add active class to curr instance btn <li> only
                location.reload();
            },
            error: function (request, error) {
                console.log("Navigating to instance failed: " + JSON.stringify(request));
            }
        });
    } catch (err) {
        console.log(err);
    }
}