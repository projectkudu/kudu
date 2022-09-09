using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kudu.Contracts.Scan
{
    public interface IScanManager
    {
        Task<ScanRequestResult> StartScan(
            String timeout,
            String mainScanDirPath,
            String id,
            String host,
            Boolean checkModified);

        Task<ScanStatusResult> GetScanStatus(
            String scanId,
            String mainScanDirPath);

        Task<ScanReport> GetScanResultFile(
            String scanId,
            String mainScanDirPath);

        IEnumerable<ScanOverviewResult> GetResults(String mainScanDir);

        void StopScan(String mainScanDirPath);
     }
}

