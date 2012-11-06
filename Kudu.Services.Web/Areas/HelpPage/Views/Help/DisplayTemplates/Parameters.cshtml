@using System.Collections.ObjectModel
@using System.Web.Http.Description
@using System.Threading
@model Collection<ApiParameterDescription>

<table>
    <thead class="ui-widget-header">
    <tr><th>Name</th><th>Description</th><th>Additional information</th></tr>
    </thead>
    <tbody class="ui-widget-content">
    @foreach (ApiParameterDescription parameter in Model)
    {
        string parameterDocumentation = parameter.Documentation != null ?
            parameter.Documentation :
            "No documentation available.";
    
        // Don't show CancellationToken because it's a special parameter
        if (!typeof(CancellationToken).IsAssignableFrom(parameter.ParameterDescriptor.ParameterType))
        {
            <tr>
                <td><b>@parameter.Name</b></td>
                <td><pre>@parameterDocumentation</pre></td>
                <td>
                    @switch (parameter.Source)
                    {
                        case ApiParameterSource.FromBody:
                            <p>Define this parameter in the request <b>body</b>.</p>
                            break;
                        case ApiParameterSource.FromUri:
                            <p>Define this parameter in the request <b>URI</b>.</p>
                            break;
                        case ApiParameterSource.Unknown:
                        default:
                            <p>None.</p>
                            break;
                    }
                </td>
            </tr>
        }
    }
    </tbody>
</table>