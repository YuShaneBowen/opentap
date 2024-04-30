using System;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    class MixinSerializer : TapSerializerPlugin
    {
        static readonly XName rootElemName = "OpenTap.Mixins";
        private static readonly XName mixinElemName = "Mixin";
        
        public override double Order => 1.1;
        
        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            var rootElem = node.Element(rootElemName);
            if (rootElem != null)
            {
                var elements = rootElem.Elements();
                void setter2(object targetObject)
                {
                    foreach (var elem in elements)
                    {
                        Serializer.Deserialize(elem, mixin =>
                        {
                            var mixin2 = (IMixinBuilder) mixin;
                            MixinFactory.LoadMixin(targetObject, mixin2);

                        }, TypeData.FromType(typeof(IMixinBuilder)));
                    }
                    setter(targetObject);
                }
                rootElem.Remove();
                return Serializer.Deserialize(node,  setter2, t);
            }
            return false;
        }

        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            if (obj == null) return false;
            var dynamicMembers = DynamicMember.GetDynamicMembers(obj);
            XElement mixins = null;
            foreach (var dynamicMember in dynamicMembers)
            {
                if (dynamicMember is MixinMemberData mixin)
                {
                    if (mixins == null)
                    {
                        mixins = node.Element(rootElemName);
                        if (mixins == null)
                        {
                            mixins = new XElement(rootElemName);
                            node.Add(mixins);
                        }
                    }
                    
                    var encodedName =Serializer.PropertyXmlName(dynamicMember.Name); 
                    var currentElement = mixins.Element(encodedName);
                    // in some rare case this already happened once. This happens because we return false below.
                    // In this case it is safe to ignore it and continue.
                    if (currentElement == null)
                    {
                        XElement elem = new XElement(encodedName);
                        mixins.Add(elem);
                        Serializer.Serialize(elem, mixin.Source);
                    }
                }
            }

            // the mixin members has been serialized now, but
            // continue serializing as before. 
            return false;
        }
    }
}