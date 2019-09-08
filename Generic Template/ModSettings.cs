using System.Collections.Generic;
using BattleTech;

namespace FullXotlTables
{
    public class ModSettings
    {
        public bool Debug = true;
        public string modDirectory;

        public bool RespectUnitLanceRole = true;
        public bool RespectUnitAIRole = true;
        public bool RespectUnitTraits = true;

        public bool IgnoreRepetitionOnce = true;
        public float CommonMechFlattenFactor = 1.0f;
        public int CommonMechFlattenFloor = 50;
    }
}
