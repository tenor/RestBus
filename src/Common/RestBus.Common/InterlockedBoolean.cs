using System.Threading;

namespace RestBus.Common
{
    /// <summary>
    /// Provides a boolean value that is written to using Interlocked.Exchange.
    /// Use this struct only as a field and access it directly from any callee.
    /// Do not pass this struct to methods/indexers etc. as it will be copied.
    /// </summary>
    public struct InterlockedBoolean
    {
        const int FALSE = 0;
        const int TRUE = -1;

        private int _value; //Will be initialized as False

        public bool IsFalse
        {
            get
            {
                return _value == FALSE;
            }
        }

        public bool IsTrue
        {
            get
            {
                return !IsFalse;
            }
        }

        public void Set(bool value)
        {
            int i_value = value == true ? TRUE : FALSE;
            Interlocked.Exchange(ref _value, i_value);
        }

        public override string ToString()
        {
            return (_value == TRUE ? true : false).ToString(); 
        }

    }
}
