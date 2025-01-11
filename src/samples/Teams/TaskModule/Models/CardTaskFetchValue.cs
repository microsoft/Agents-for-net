// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace TaskModule.Models
{
    public class CardTaskFetchValue<T>
    {
       
        public object Type { get; set; } = "task/fetch";

      
        public T Data { get; set; }
    }
}
