using System;

namespace ObjectConvert
{
    [AttributeUsage(AttributeTargets.Field)]
    public class FixedStringAttribute : Attribute
    {
        private int length_;

        public FixedStringAttribute(int length)
        {
            this.length_ = length;
        }

        public int length
        {
            get
            {
                return this.length_;
            }
        }
    }
}
