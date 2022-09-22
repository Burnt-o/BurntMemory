using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurntMemory
{
    public partial class ReadWrite
    {
        public class Pointer
        {
            public string? Modulename;
            public int[]? Offsets;
            public IntPtr? Address;
            public IntPtr? BaseAddress;

            // Pointer class has various constructor overloads. ResolvePointer method also has matching overloads to deal with these, all of which returning an IntPtr?.
            public Pointer(string? a, int[]? b)
            {
                this.Modulename = a;
                this.Offsets = b;
                this.Address = null;
                this.BaseAddress = null;
            }

            public Pointer(IntPtr? a, int[]? b)
            {
                this.Modulename = null;
                this.Offsets = b;
                this.Address = null;
                this.BaseAddress = a;
            }

            public Pointer(IntPtr? a)
            {
                this.Modulename = null;
                this.Offsets = null;
                this.Address = a;
                this.BaseAddress = null;
            }

            public Pointer(int[]? a)
            {
                this.Modulename = null;
                this.Offsets = a;
                this.Address = null;
                this.BaseAddress = null;
            }

            public Pointer(string? a, int[]? b, IntPtr? c, IntPtr? d) // Used for deepcopy in +operator
            {
                this.Modulename = a;
                this.Offsets = b;
                this.Address = c;
                this.BaseAddress = d;
            }

            // A static method for adding an offset to a Pointer;
            // For a Pointer object that contains a non-null int[] Offsets, the offset is added to the last int of the Offsets array.
            // For a Pointer object that has a null int[] Offsets, the offset is instead added to the IntPtr (Address, not BaseAddress)
            public static Pointer? operator +(Pointer? originalPointer, int? addOffset)
            {
                if (originalPointer == null || addOffset == null)
                {
                    return originalPointer;
                }



                Pointer newPointer; // A new Pointer object that will be a clone of originalPointer, then modified and finally returned.
                if (originalPointer.Offsets != null) // We need to add the addOffset to the last element of int[] Offsets
                {
                    newPointer = new(originalPointer.Modulename, (int[])originalPointer.Offsets.Clone(), originalPointer.Address, originalPointer.BaseAddress); // Clone the originalPointer
                    int? lastElement = newPointer.Offsets[^1]; // Get the last element of Offsets field

                    lastElement += addOffset; // Add addOffset to last element
                    newPointer.Offsets[^1] = lastElement.GetValueOrDefault(); // Update copied Pointer's last element
                    return newPointer; // And return the modified Pointer
                }
                else if (originalPointer.Address != null) // We need to add the addOffset to the Address IntPtr
                {
                    newPointer = new(originalPointer.Modulename, null, originalPointer.Address, originalPointer.BaseAddress); // Clone the originalPointer (but this time we know the Offsets field is null)

                    IntPtr newAddress = new IntPtr((int)originalPointer.Address + (int)addOffset); // Add offset to IntPtr
                    newPointer.Address = newAddress; // Update cloned Pointer's Address field with the newAddress
                    return newPointer; // And return the modified Pointer
                }

                //Execution shouldn't normally get here unless the Pointer had both null Offsets and Address, but just in case
                return originalPointer;
            }
        }
    }
}
