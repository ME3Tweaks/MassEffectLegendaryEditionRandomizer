using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Unreal;

namespace Randomizer.Randomizers.Shared.Classes
{
    public static class SharedExtensions
    {

        /// <summary>
        /// Builds an enumeration of all properties in this collection, including the children properties. DO NOT ACCESS .Properties on arrays or structs - as they are also added to this list
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static IEnumerable<Property> GetAllProperties(this List<Property> collection)
        {
            List<Property> props = new List<Property>();
            props.AddRange(collection);
            foreach (var subProp in collection)
            {
                if (subProp is ArrayPropertyBase apb)
                {
                    props.AddRange(GetAllProperties(apb.Properties.ToList()));
                }
                else if (subProp is StructProperty sp)
                {
                    props.AddRange(GetAllProperties(sp.Properties.ToList()));
                }
                else
                {
                }
            }

            return props;
        }
    }
}
