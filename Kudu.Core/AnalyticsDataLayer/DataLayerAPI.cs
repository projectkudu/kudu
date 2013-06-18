using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsDataLayer
{
    public interface IDataLayerAPI
    {
        /*
        /// <summary>
        /// Count the number of unique sessions dependent on given parameters
        /// </summary>
        /// <param name="from">Requested beginning time</param>
        /// <param name="to">Requested end time</param>
        /// <param name="unitOfTime">Will help in determining how to structure returned Dictionary object. Hourly, Daily, Weekly, Monthly</param>
        /// <returns>Dictionary whose keys are relative to the form of time requested </returns>
        Dictionary<DateTime, int> UniqueSessions(DateTime from, DateTime to, string unitOfTime);



        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Requested start date of logged cookies</param>
        /// <param name="to">Requested end date for logged cookies</param>
        /// <param name="unitOfTime">Requested unit of time to go by: Hourly, Daily, etc. Determines how we want to structure the data</param>
        /// <returns></returns>
        Dictionary<DateTime, System.Net.CookieCollection> UserCookies(DateTime from, DateTime to, string unitOfTime);

        Dictionary<string, string[]> UserSiteVisited(DateTime from, DateTime to);

        Dictionary<int,KeyValuePair<DateTime, int>> Conversions(DateTime from, DateTime to, string unitOfTime, Uri pageX, Uri pageY);*/

        /// <summary>
        /// Using the start time and end time to filter the data that we need for Metric computation
        /// </summary>
        /// <param name="start">A beginning time that the user specifies</param>
        /// <param name="end">An end time to stop retrieving data </param>
        /// <returns>Yield return an instance of IEnumerable to work on the raw data as the foreach loop is iterating</returns>
        IEnumerable<W3C_Extended_Log> GetLines(DateTime start, DateTime end);
    }
}
