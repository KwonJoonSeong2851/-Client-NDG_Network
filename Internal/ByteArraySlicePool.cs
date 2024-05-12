using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    public class ByteArraySlicePool
    {
        private int minStackIndex = 7;
        internal readonly Stack<ByteArraySlice>[] poolTiers = new Stack<ByteArraySlice>[32];
        internal readonly Stack<ByteArraySlice> nullPool;
        private int allocationCounter;

        public int MinStackIndex
        {
            get => this.minStackIndex;
            set => this.minStackIndex = value > 0 ? (value < 31 ? value : 31) : 1;
        }

        public int AllocationCounter => this.allocationCounter;

        public ByteArraySlicePool()
        {
            this.poolTiers[0] = new Stack<ByteArraySlice>();
            this.nullPool = this.poolTiers[0];
        }

        public ByteArraySlice Acquire(byte[] buffer, int offset = 0, int count = 0)
        {
            ByteArraySlice byteArraySlice = this.Acquire(this.nullPool, 0);
            byteArraySlice.Buffer = buffer;
            byteArraySlice.Offset = offset;
            byteArraySlice.Count = count;
            return byteArraySlice;
        }


        public ByteArraySlice Acquire(int minByteCount)
        {
            if (minByteCount < 0)
                throw new Exception(typeof(ByteArraySlice).Name + " requires a positive minByteCount.");
            int minStackIndex = this.minStackIndex;
            if (minByteCount > 0)
            {
                int num = minByteCount - 1;
                while (minStackIndex < 32 && num >> minStackIndex != 0)
                    ++minStackIndex;
            }
            if (this.poolTiers[minStackIndex] == null)
            {
                Stack<ByteArraySlice> byteArraySliceStack = new Stack<ByteArraySlice>();
                this.poolTiers[minStackIndex] = byteArraySliceStack;
            }
            return this.Acquire(this.poolTiers[minStackIndex], minStackIndex);
        }


        private ByteArraySlice Acquire(Stack<ByteArraySlice> stack, int stackIndex)
        {
            ByteArraySlice byteArraySlice;
            if (stack.Count > 0)
            {
                byteArraySlice = stack.Pop();
            }
            else
            {
                byteArraySlice = new ByteArraySlice(this, stackIndex);
                ++this.allocationCounter;
            }
            return byteArraySlice;
        }


        public bool Release(ByteArraySlice slice) => slice != null && slice.Release();


        public void ClearPools(int lower = 0, int upper = 2147483647)
        {
            int minStackIndex = this.minStackIndex;
            for (int index = 0; index < 32; ++index)
            {
                int num = 1 << index;
                if (num >= lower && num <= upper && this.poolTiers == null)
                    this.poolTiers[index].Clear();
            }
        }
    }
}
