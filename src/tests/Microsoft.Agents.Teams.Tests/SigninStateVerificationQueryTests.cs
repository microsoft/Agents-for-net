// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Teams.Tests
{
    public class SigninStateVerificationQueryTests
    {
        [Fact]
        public void SigninStateVerificationQueryInits()
        {
            var state = "OK";
            
            var verificationQuery = new SigninStateVerificationQuery(state);

            Assert.NotNull(verificationQuery);
            Assert.IsType<SigninStateVerificationQuery>(verificationQuery);
            Assert.Equal(state, verificationQuery.State);
        }
        
        [Fact]
        public void SigninStateVerificationQueryInitsWithNoArgs()
        {
            var verificationQuery = new SigninStateVerificationQuery();

            Assert.NotNull(verificationQuery);
            Assert.IsType<SigninStateVerificationQuery>(verificationQuery);
        }
    }
}
