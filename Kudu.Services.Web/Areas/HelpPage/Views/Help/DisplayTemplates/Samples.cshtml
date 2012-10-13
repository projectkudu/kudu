@using System.Net.Http.Headers
@model Dictionary<MediaTypeHeaderValue, object>

@{
    // Group the samples into a single tab if they are the same.
    Dictionary<string, object> samples = Model.GroupBy(pair => pair.Value).ToDictionary(
        pair => String.Join(", ", pair.Select(m => m.Key.ToString()).ToArray()), 
        pair => pair.Key);
    var mediaTypes = samples.Keys;
}
<div class="mediaTypeTabs">
    <ul>
    @{ int id = 0; }
    @foreach (var mediaType in mediaTypes)
    {
        <li><a href="#@ViewData["sampleClass"]_@(id++)">@mediaType</a></li>
    }
    </ul>
    @{ id = 0; }
    @foreach (var mediaType in mediaTypes)
    {
        <div id="@ViewData["sampleClass"]_@(id++)">
            <h3>Sample:</h3>
            @{
                var sample = samples[mediaType];
                if (sample == null)
                {
                    <p>Sample not available.</p>
                }
                else
                {
                    @Html.DisplayFor(s => sample);
                }
            }
        </div>
    }
</div>