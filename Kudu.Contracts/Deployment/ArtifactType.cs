using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Contracts.Deployment
{
    public enum ArtifactType
    {
        Invalid,
        War,
        Jar,
        Lib,
        Static,
        Startup,
        Ear,
        Zip
    }
}
