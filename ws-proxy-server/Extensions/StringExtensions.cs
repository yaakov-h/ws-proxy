namespace WebSocketProxy.Server
{
    static class StringExtensions
    {
        public static bool SlowEquals(this string current, string other)
        {
            if (current == null ^ other == null)
            {
                // Only one of them is null, we don't care which.
                return false;
            }

            if (ReferenceEquals(current, other))
            {
                // This also handles nulls.
                return true;
            }

            if (current.Length != other.Length)
            {
                return false;
            }

            var result = true;
            for (var i = 0; i < current.Length; i++)
            {
                var c = current[i];
                var o = other[i];

                result &= c.Equals(o);
            }

            return result;
        }
    }
}
