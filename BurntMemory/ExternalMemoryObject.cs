using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurntMemory
{
    public class ExternalMemoryObject
    {
        public ExternalMemoryObject(ReadWrite.Pointer? pointer = null, Field[]? fields = null)
        {
            this.Pointer = pointer;
            this.Fields = fields;
        }

        public Field[]? Fields
        { get; set; }

        public ReadWrite.Pointer? Pointer
        { get; set; }

    }
}
