using System.Threading;

namespace RestBus.Common
{
    /// <summary>
    /// Provides a boolean value that is written to using Interlocked.Exchange.
    /// Use this struct only as a field in a class and access it directly from any callee.
    /// Do not pass this struct to methods/indexers etc. as it will be copied.
    /// </summary>
    /// <remarks>
    /// Consider using a volatile bool field instead, unless you need CompareExchange (SetIf) operations.
    /// </remarks>
    public struct InterlockedBoolean
    {
        const int FALSE = 0;
        const int TRUE = -1;
        private int _value; //Will be initialized as False

        /// <summary>
        /// Returns True, if the value is false
        /// </summary>
        public bool IsFalse
        {
            get
            {
                return _value == FALSE;
            }
        }

        /// <summary>
        /// Returns True, if the value is false
        /// </summary>        
        public bool IsTrue
        {
            get
            {
                return !IsFalse;
            }
        }

        /// <summary>
        /// Sets the value to a specified value
        /// </summary>
        /// <param name="value">The new value</param>
        public void Set(bool value)
        {
            int i_value = value == true ? TRUE : FALSE;
            Interlocked.Exchange(ref _value, i_value);
        }

        /// <summary>
        /// Sets to a specified new value if the existing value matches a specified value
        /// </summary>
        /// <param name="valueEquals">The compared value</param>
        /// <param name="newValue">The new value</param>
        /// <returns>True if the set operation succeeded, false otherwise</returns>
        public bool SetIf(bool valueEquals, bool newValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue ? TRUE : FALSE, valueEquals ? TRUE : FALSE) == (valueEquals ? TRUE : FALSE);
        }

        /// <summary>
        /// Sets to true if the existing value matches a specified value
        /// </summary>
        /// <param name="valueEquals"></param>
        /// <returns>True if the set operation succeeded, false otherwise</returns>
        public bool SetTrueIf(bool valueEquals)
        {
            return SetIf(valueEquals, true);
        }

        /// <summary>
        /// Sets to false if the existing value matches a specified value
        /// </summary>
        /// <param name="valueEquals"></param>
        /// <returns>True if the set operation succeeded, false otherwise</returns>
        public bool SetFalseIf(bool valueEquals)
        {
            return SetIf(valueEquals, false);
        }

        public override string ToString()
        {
            return (_value == TRUE ? true : false).ToString(); 
        }

    }
}
