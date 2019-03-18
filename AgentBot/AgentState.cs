// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AgentBot
{
    /// <summary>
    /// Stores counter state for the conversation.
    /// Stored in <see cref="Microsoft.Bot.Builder.ConversationState"/> and
    /// backed by <see cref="Microsoft.Bot.Builder.MemoryStorage"/>.
    /// </summary>
    public class AgentState
    {
        /// <summary>
        /// Gets or sets the number of turns in the conversation.
        /// </summary>
        /// <value>The number of turns in the conversation.</value>
        public int TurnCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the wallet key.
        /// </summary>
        /// <value>The wallet key.</value>
        public string WalletKey { get; set; }

        /// <summary>
        /// Gets or sets the wallet identifier.
        /// </summary>
        /// <value>The wallet identifier.</value>
        public string WalletId { get; set; }
    }
}
