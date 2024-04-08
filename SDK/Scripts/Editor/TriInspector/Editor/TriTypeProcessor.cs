using System;
using System.Collections.Generic;

namespace TriInspectorMVCE
{
    public abstract class TriTypeProcessor
    {
        public abstract void ProcessType(Type type, List<TriPropertyDefinition> properties);
    }
}