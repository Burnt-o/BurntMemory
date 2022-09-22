using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurntMemory
{
    public class Field
    {
        public Field(int offset, Field[]? fields = null, Field? parentfield = null)
        {
            this.Offset = parentfield != null ? offset + parentfield.Offset : offset;
            this.Fields = fields;
        }

        public int Offset
        { get; set; }


        public Field[]? Fields
        { get; set; }

        public static explicit operator Field(int v)
        {
            return new Field(v);
        }
    }
}
