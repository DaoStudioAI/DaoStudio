using System;
using System.Security.Cryptography;
using System.Threading;

namespace DaoStudio.DBStorage.Common
{
    /// <summary>
    /// Generates unique long IDs for database entities
    /// </summary>
    public static class IdGenerator
    {
        private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private static readonly long _timeOffset = new DateTime(2023, 1, 1).Ticks;
        private static int _sequence = 0;

        /// <summary>
        /// Generates a unique long ID based on current time with a random component
        /// </summary>
        /// <returns>A unique long ID</returns>
        public static long GenerateId()
        {
            long timestamp = (DateTime.UtcNow.Ticks - _timeOffset) >> 16;
            int currentSequence = Interlocked.Increment(ref _sequence) & 0xFFFF;
            
            byte[] randomBytes = new byte[4];
            _rng.GetBytes(randomBytes);
            int random = BitConverter.ToInt32(randomBytes, 0) & 0x7FFFFF;
            
            // Combine timestamp (high 42 bits), sequence (next 16 bits), and random (low 23 bits)
            long id = (timestamp << 39) | ((long)currentSequence << 23) | (long)random;
            
            return id;
        }

        /// <summary>
        /// Checks if the ID already exists in the database
        /// </summary>
        /// <typeparam name="T">The type of the existence check function</typeparam>
        /// <param name="existsFunc">Function that checks if the ID exists</param>
        /// <returns>A unique long ID that doesn't exist in the database</returns>
        public static long GenerateUniqueId(Func<long, bool> existsFunc)
        {
            long id;
            int attempts = 0;
            const int maxAttempts = 10;

            do
            {
                id = GenerateId();
                attempts++;

                if (attempts >= maxAttempts)
                {
                    throw new InvalidOperationException($"Failed to generate a unique ID after {maxAttempts} attempts");
                }
            }
            while (existsFunc(id));

            return id;
        }
    }
} 