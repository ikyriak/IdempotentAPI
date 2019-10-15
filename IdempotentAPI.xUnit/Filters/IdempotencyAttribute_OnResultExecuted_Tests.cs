using IdempotentAPI.Filters;
using IdempotentAPI.xUnit.ApplicationServices.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using Xunit;

namespace IdempotentAPI.xUnit.Filters
{
    public class IdempotencyAttribute_OnResultExecuted_Tests
    {

    }
}
