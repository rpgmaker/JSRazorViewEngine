using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSRazorViewEngine.Runtime {
    internal unsafe class RazorHelper {

        internal unsafe static string GetString(char* chrs, int count) {
            var size = count - 1;
            var str = new string('\0', size);
            fixed (char* cp = str)
                memcpy(cp, chrs - count, size);
            return str;
        }

        internal static bool IsEqual(char[] chr1, char[] chr2) {

            if (chr1.Length != chr2.Length) return false;
            if (chr1 == null && chr2 == null) return true;

            for (var i = 0; i < chr1.Length; i++) {
                if (chr1[i] != chr2[i])
                    return false;
            }

            return true;
        }

        internal static unsafe void memcpy(char* dmem, char* smem, int charCount) {
            if ((((int)dmem) & 2) != 0) {
                dmem[0] = smem[0];
                dmem++;
                smem++;
                charCount--;
            }
            while (charCount >= 8) {
                *((int*)dmem) = *((int*)smem);
                *((int*)(dmem + 2)) = *((int*)(smem + 2));
                *((int*)(dmem + 4)) = *((int*)(smem + 4));
                *((int*)(dmem + 6)) = *((int*)(smem + 6));
                dmem += 8;
                smem += 8;
                charCount -= 8;
            }
            if ((charCount & 4) != 0) {
                *((int*)dmem) = *((int*)smem);
                *((int*)(dmem + 2)) = *((int*)(smem + 2));
                dmem += 4;
                smem += 4;
            }
            if ((charCount & 2) != 0) {
                *((int*)dmem) = *((int*)smem);
                dmem += 2;
                smem += 2;
            }
            if ((charCount & 1) != 0) {
                dmem[0] = smem[0];
            }
        }
    }
}
