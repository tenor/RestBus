//Sourced from https://aspnetwebstack.codeplex.com/SourceControl/latest#src/System.Net.Http.Formatting/Formatting/IRequiredMemberSelector.cs
// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace RestBus.Client.Http.Formatting
{
    using System.Reflection;

    /// <summary>
    /// Interface to determine which data members on a particular type are required.
    /// </summary>
    internal interface IRequiredMemberSelector
    {
        /// <summary>
        /// Determines whether a given member is required on deserialization.
        /// </summary>
        /// <param name="member">The <see cref="MemberInfo"/> that will be deserialized.</param>
        /// <returns><c>true</c> if <paramref name="member"/> should be treated as a required member, otherwise <c>false</c>.</returns>
        bool IsRequiredMember(MemberInfo member);
    }
}
