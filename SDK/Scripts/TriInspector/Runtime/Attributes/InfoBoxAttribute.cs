using System;
using System.Diagnostics;

namespace TriInspectorMVCE
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class InfoBoxAttribute : Attribute
    {
        public string Text { get; }
        public TriMessageType MessageType { get; }
        public string VisibleIf { get; }

        public InfoBoxAttribute(string text, TriMessageType messageType = TriMessageType.Info, string visibleIf = null)
        {
            Text = text;
            MessageType = messageType;
            VisibleIf = visibleIf;
        }
    }
}