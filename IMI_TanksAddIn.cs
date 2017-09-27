using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MapInfo.Types;

namespace MI_Tanks
{
    interface IMI_TanksAddIn
    {
        // This function should be called from MapBasic Subroutine - Main - to initialize the AddIn.
        void Initialize(IMapInfoPro mapInfoApplication, string mbxname);
        // This function should be called from MapBasic Special handler   - EndHandler - to exit the AddIn and perform the cleanup.
        void Unload();
        // This is the main MapInfoPro interface which provides methods for Mapbasic applicatiions to communicate with MapInfo Pro.
        IMapBasicApplication ThisApplication { get; set; }
    }
}
