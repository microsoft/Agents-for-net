// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.App
{
    public class SignInResolver : List<string>
    {
        public delegate string[] Delegate(ITurnContext turnContext);

        private static readonly char[] separator = [',', ';', ' '];
        private readonly Delegate _delegate;

        public SignInResolver()
        {

        }

        public SignInResolver(string[] list) : base(list)
        {
        }

        public SignInResolver(IEnumerable<string> list) : base(list)
        {
        }

        public SignInResolver(string delimitedList) :base(delimitedList.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
        }

        public SignInResolver(Delegate del, string[] staticList = null) : base(staticList ?? [])
        {
            _delegate = del;
        }

        public static implicit operator SignInResolver(string? value)
        {
           return new SignInResolver(value);
        }

        public static implicit operator SignInResolver(string[] value)
        {
           return new SignInResolver(value);
        }

        public static implicit operator SignInResolver(Delegate value)
        {
            return new SignInResolver(value);
        }

        public virtual string[] Resolve(ITurnContext turnContext)
        {
            if (_delegate != null)
            {
                return _delegate(turnContext);
            }
            var list = new List<string>(this);
            if (_delegate != null)
            {
                list.AddRange(_delegate(turnContext));
            }
            return [.. list];
        }
    }
}
