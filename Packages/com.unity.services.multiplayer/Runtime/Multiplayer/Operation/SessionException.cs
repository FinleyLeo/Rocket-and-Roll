using System;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// The exception for session operations
    /// </summary>
    public class SessionException : Exception
    {
        /// <summary>
        /// Gets the error type
        /// </summary>
        public SessionError Error { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="error">The exception type.</param>
        internal SessionException(string message, SessionError error) : base(message)
        {
            Error = error;
        }

        /// <summary>
        /// Returns a string representation of the
        /// <see cref="SessionException"/>instance.
        /// </summary>
        /// <returns>A string that represents the current exception with its
        /// <see cref="SessionError"/> and its <see cref="Exception.Message"/>.</returns>
        public override string ToString()
        {
            return $"SessionException: [Error: {Error}] [Message: {Message}]";
        }
    }
}
