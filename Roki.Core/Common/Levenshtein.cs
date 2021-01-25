namespace Roki.Common
{
    // Copyright (c) 2017 DanHartley

    /// <summary>
    ///     Measures the difference between two strings.
    ///     Uses the Levenshtein string difference algorithm.
    /// </summary>
    public class Levenshtein
    {
        private readonly int[] _costs;
        /*
         * WARRING this class is performance critical (Speed).
         */

        private readonly string _storedValue;

        /// <summary>
        ///     Creates a new instance with a value to test other values against
        /// </summary>
        /// <param name="value">Value to compare other values to.</param>
        public Levenshtein(string value)
        {
            _storedValue = value;
            // Create matrix row
            _costs = new int[_storedValue.Length];
        }

        /// <summary>
        ///     gets the length of the stored value that is tested against
        /// </summary>
        public int StoredLength => _storedValue.Length;

        /// <summary>
        ///     Compares a value to the stored value.
        ///     Not thread safe.
        /// </summary>
        /// <returns>Difference. 0 complete match.</returns>
        public int DistanceFrom(string value)
        {
            if (_costs.Length == 0) return value.Length;

            // Add indexing for insertion to first row
            for (var i = 0; i < _costs.Length;) _costs[i] = ++i;

            for (var i = 0; i < value.Length; i++)
            {
                // cost of the first index
                int cost = i;
                int additionCost = i;

                // cache value for inner loop to avoid index lookup and bonds checking, profiled this is quicker
                char value1Char = value[i];

                for (var j = 0; j < _storedValue.Length; j++)
                {
                    int insertionCost = cost;

                    cost = additionCost;

                    // assigning this here reduces the array reads we do, improvement of the old version
                    additionCost = _costs[j];

                    if (value1Char != _storedValue[j])
                    {
                        if (insertionCost < cost) cost = insertionCost;

                        if (additionCost < cost) cost = additionCost;

                        ++cost;
                    }

                    _costs[j] = cost;
                }
            }

            return _costs[^1];
        }
    }
}