/// <reference path="http://ajax.aspnetcdn.com/ajax/jQuery/jquery-2.0.2.min.js"/>
/// <reference path="jquery.jqplot.min.js"/>
var metricResultsJson;
var cosPoints = [];

//Array of metrics with their name, descriptions, and parameters
var metricInformation = new Array();
var allChartDivs = new Array();

//for JQplot, customize according to the days selected
var plot;
function formPlotData() {
    var plotData = [];
    var xVal, yVal;
    var x = 1;
    var y = 2;
    var metricJSON = JSON.parse(metricResultsJson, function (k, v) {
        if ((k === "Value" && typeof (v) === 'number')) {
            yVal = v;
        }
        else if (k === "Key" && typeof (v) === 'string') {
            
            xVal = v;
        }

        if (xVal != undefined && yVal != undefined) {
            //push to the array
            plotData.push([xVal, yVal]);
            xVal = undefined;
            yVal = undefined;
            x++;
        }

    });

    return plotData;
}

var resources = {
    getMetricsURL: "diagnostics/analytics/metrics",
    chartDivs: new Array(),
};

/*
Get all the available metrics that the Analytics API supports.
*/
function getAvailableMetrics() {
    var arrayMetrics;
    //use jQuery AJAX getJSON call to retrieve data from the Analytics API (note Analytics API returns JSON serialized data)
    $.getJSON(resources.getMetricsURL, function (data) {
        var count = 0;
        var name;
        var metric;
        JSON.parse(data, function (k, v) {
            if (k === "Name") {
                name = v;
            }
            else if (k === "Description") {
                //add the metric to the data;
                addMetric(new Metric(name, v, ""));
            }
        });
    })
    .done(function () { //After the the AJAX call is done do the following
        //as soon as the parsing is done run this function
        ko.applyBindings(new ViewModel());

        //use forEach to iterate and use jQuery to create div tags inside of the div id= analytics area, cant use knockouts.foreach binding because it creates mutiple div tags with the same identifier
        var count = 1;
        var divIdentifier = "chart" + count;
        var metricDiv = '<div id=' + divIdentifier + '></div>';
        var divContainer = "#analyticsArea";
        //iterate through each metric information
        metricInformation.forEach(function (metric) {
            //add the name
            $(divContainer).append('<h1>' + metric.name + '</h1>');
            //add the description of the metric
            $(divContainer).append('<h3>' + metric.description + '</h3>');

            //add some styling
            $(divContainer).append(metricDiv).addClass("analyticsChartStyle");
            //$.jqplot(divIdentifier, [[[1, 2], [3, 5.12], [5, 13.1], [7, 33.6], [9, 85.9], [11, 219.9]]]);

            //add the div identifier into an array to keep track of all the div containers that we need to populate with jQplot data
            resources.chartDivs.push({"metric":metric.name, "divContainer":divIdentifier});
            //iterate the count for the next div identifier
            count++;
            divIdentifier = "chart" + count;
            metricDiv = '<div id=' + divIdentifier + '></div>';
        });


    });
}



//Depending on the time interval selected, we will spark an api call to retrieve information for each metric available
function apiCall(time) {
    var begin = new Date();
    var timeInterval, tickInterval;
    switch (time) {
        case 1:
            //this sets by the day
            begin.setDate(begin.getDate() - 7);
            timeInterval = "1";
            tickInterval = "1 day";
            break;
        case 2:
            //this sets it to 4 hour intervals
            begin.setHours(begin.getHours() - 24);
            timeInterval = "1:00"
            tickInterval = "1 hour";
            break;
        case 3:
            begin.setMonth(begin.getMonth() - 1);
            timeInterval = "1";
            tickInterval = "7 day"
            break;
    }
    var jsonArguments = new Object();
    jsonArguments.statuscode = "200";
    var current = new Date();
    current.setDate(current.getDate() + 2)
    callAPI("StatusCodeMetric", begin.toDateString(), current.toDateString(), timeInterval, JSON.stringify(jsonArguments), tickInterval);
}

//metricList :: list of metrics user wants to get: StatusCode, Conversions, etc..
//startTime :: a string that is in C# DateTime syntax that denotes the beginning of the data
//endTime :: a string that is in C# DateTime syntax that denotes the ending of the data
//intervalTime :: a string that is in C# TimeInterval syntax to denote the time interval between start time and end time
//metricArguments :: arguments that are parameters for the requested metrics
function callAPI(metricList, startTime, endTime, intervalTime, metricArguments, tickInterval) {
    var apiURL = "diagnostics/analytics";
    var request = "metric"
    var result;
    $.get(apiURL, { metrics: metricList, start: startTime, end: endTime, interval: intervalTime, arguments: metricArguments },
        function callback(data, textStatus) {
            metricResultsJson = data;
            result = formPlotData();
            SetJQPLOT(tickInterval, result);
        })
}

function SetJQPLOT(requestedTickInterval, result) {
    plot = $.jqplot('analyticsChart', [result], {
        series: [{
            showMarker: true,
            pointLabels: {show:true}}],
        axes: {
            xaxis: {
                renderer: $.jqplot.DateAxisRenderer,
                label: "Status Codes of 200s",
                tickInterval: requestedTickInterval
            }
        },
    });
}

//our ViewModel for different units of TIME that a user can use for the Analytics tool
function ViewModel(){
    var self = this;
    self.unitTimes = ko.observableArray(['7 days', '24 Hours', '6 Hours', 'Month']);
    self.items = ko.observableArray([[[1, 2], [3, 5.12], [5, 13.1], [7, 33.6], [9, 85.9], [11, 219.9]]]);
    //unit of time the user selected.
    self.selectedTime = ko.observable();
    //array of the metrics that are available on the API
    self.metrics = ko.observableArray(metricInformation);
    self.addMetric = function () {
        self.metrics.push('fdsfds');
    }

    //self.setMyPlot = $.jqplot('analyticsChart', [[[1, 2], [3, 5.12], [5, 13.1], [7, 33.6], [9, 85.9], [11, 219.9]]]);

};

///Add metrics to the metric array.
function addMetric(metric) {
    metricInformation.push(metric);
}

function Metric(name, description, parameters) {
    this.name = name;
    this.description = description;
    this.parameters = parameters;
}

//custom binding for the charts
ko.bindingHandlers.analytics_chart = {
    init: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
    },

    update: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
        //get all the chart values
        var values = ko.unwrap(valueAccessor());
        var dataPlotValues = [[]];
        $.jqplot(element.id, [[[1, 2], [3, 5.12], [5, 13.1], [7, 33.6], [9, 85.9], [11, 219.9]]]);
    }
};

ko.bindingHandlers.chart = {
    init: function (element, valueAccessor, allBindingsAccessor, viewModel) {
        // empty - left as placeholder if needed later
    },
    update: function (element, valueAccessor, allBindingsAccessor, viewModel) {
        // prepare chart values
        var items = ko.utils.unwrapObservable(valueAccessor);
        var chartValues = [[]];
        for (var i = 0; i < items().length; i++) {
            chartValues[0].push(items()[i].totalOunces());
        }

        // clear previous chart
        $(element).html("");
        $.jqplot(element.id, [[[1, 2], [3, 5.12], [5, 13.1], [7, 33.6], [9, 85.9], [11, 219.9]]], {
            title: 'Baby Weight'
        });
    }
};





