using System;
using System.Threading;

namespace RestBus.Common
{
    /// <summary>
    /// Generates numbers and ids sequentially in a thread-safe manner
    /// </summary>
    public class SequenceGenerator
    {
        bool _excludeNegativeNumbers;
        long _current;

        /// <summary>
        /// Initializes a new <see cref="SequenceGenerator"/>
        /// </summary>
        /// <param name="startValue">Value to start generating sequences from</param>
        /// <param name="excludeNegativeNumbers">Set to true to always generate positive numbers, false to include the negative number range</param>
        public SequenceGenerator(long startValue, bool excludeNegativeNumbers)
        {
            if (!excludeNegativeNumbers && startValue < 0) throw new ArgumentException("startValue argument is negative while excludeNegativeNumbers argument is set to true!");
            this._excludeNegativeNumbers = excludeNegativeNumbers;
            _current = startValue - 1;
        }

        public static SequenceGenerator FromUtcNow()
        {
            return new SequenceGenerator(DateTime.UtcNow.Ticks, false);
        }

        public long GetNext()
        {
            var val = Interlocked.Increment(ref _current);

            if(val < 0 && _excludeNegativeNumbers)
            {
                do
                {
                    if (Interlocked.CompareExchange(ref _current, 0, val) == val)
                    {
                        //Current was successfully set to 0;
                        val = 0;
                        break;
                    }
                    else
                    {
                        //A different thread changed _current, so increment and read again
                        val = Interlocked.Increment(ref _current);
                    }
                }
                while (val < 0);
            }

            return val;
        }

        public string GetNextId()
        {
            return GetNext().ToString("x");
        }

    }
}
