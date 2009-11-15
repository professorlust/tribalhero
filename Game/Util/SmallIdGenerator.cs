using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;

namespace Game.Util {
    public class SmallIdGenerator {
        BitArray bitarray;
        int start = 1;
        bool fetchLowest;

        public SmallIdGenerator(int max) : this(max, false) {            
        }

        public SmallIdGenerator(int max, bool fetchLowest) {
            this.fetchLowest = fetchLowest;
            bitarray = new BitArray(max, false);
        }

        public int getNext() {
            lock (bitarray) {
                int size = bitarray.Length;                
                for (int i = 0; i < size; ++i) {
                    int idx; 
                    if (fetchLowest)
                        idx = i;
                    else
                        idx = (i + start) % bitarray.Length;
                                        
                    if (idx == 0) continue;

                    if (bitarray.Get(idx) == false) {
                        bitarray.Set(idx, true);
                        start = idx;
                        return idx;
                    }
                }
            }

            return -1;
        }

        public void set(int id) {
            lock (bitarray) {
                bitarray.Set(id, true);
            }
        }

        public void release(int id) {
            lock (bitarray) {
                bitarray.Set(id, false);
            }
        }
    }
}
