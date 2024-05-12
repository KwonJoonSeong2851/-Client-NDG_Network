using System;
using System.Collections.Generic;


namespace NDG
{
    public class Pool<T> where T : class
    {
        private readonly Func<T> createFunction;
        private readonly Queue<T> pool;
        private readonly Action<T> resetFunction;

        public Pool(Func<T> createFunction, Action<T> resetFunction, int poolCapacity)
        {
            this.createFunction = createFunction;
            this.resetFunction = resetFunction;
            this.pool = new Queue<T>();
            this.CreatePoolItems(poolCapacity);
        }

        public Pool(Func<T> createFunction, int poolCapacity)
          : this(createFunction, (Action<T>)null, poolCapacity)
        {
        }

        public int Count
        {
            get
            {
                lock (this.pool)
                    return this.pool.Count;
            }
        }

        private void CreatePoolItems(int numItems)
        {
            for (int index = 0; index < numItems; ++index)
                this.pool.Enqueue(this.createFunction());
        }



        public void Release(T item)
        {
            if ((object)item == null)
                throw new ArgumentNullException("item 값이 null입니다.");
            if (this.resetFunction != null)
                this.resetFunction(item);
            lock (this.pool)
                this.pool.Enqueue(item);
        }



        public T Acquire()
        {
            T obj;
            lock (this.pool)
            {
                if (this.pool.Count == 0)
                    return this.createFunction();
                obj = this.pool.Dequeue();
            }
            return obj;
        }
    }
}
