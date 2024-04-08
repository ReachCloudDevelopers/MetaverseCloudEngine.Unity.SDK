using TriInspectorMVCE;
using TriInspectorMVCE.Drawers;
using TriInspectorMVCE.Elements;
using TriInspectorMVCE.Utilities;
using TriInspectorUnityInternalBridgeMVCE;

[assembly: RegisterTriValueDrawer(typeof(CustomBuiltInDrawer), TriDrawerOrder.Fallback - 999)]

namespace TriInspectorMVCE.Drawers
{
    public class CustomBuiltInDrawer : TriValueDrawer<object>
    {
        public override TriElement CreateElement(TriValue<object> propertyValue, TriElement next)
        {
            var property = propertyValue.Property;

            if (property.TryGetSerializedProperty(out var serializedProperty))
            {
                var handler = ScriptAttributeUtilityProxy.GetHandler(serializedProperty);

                var drawWithHandler = handler.hasPropertyDrawer ||
                                      property.PropertyType == TriPropertyType.Primitive ||
                                      TriUnityInspectorUtilities.MustDrawWithUnity(property);

                if (drawWithHandler)
                {
                    return new TriBuiltInPropertyElement(property, serializedProperty, handler);
                }
            }

            return base.CreateElement(propertyValue, next);
        }
    }
}