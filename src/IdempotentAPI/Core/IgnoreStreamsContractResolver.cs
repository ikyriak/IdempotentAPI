using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace IdempotentAPI.Core;

public class IgnoreHttpContextContractResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        return
            base.CreateProperties(type, memberSerialization)
                .Where(x => x.DeclaringType == null ||
                            (!x.DeclaringType.Name.Contains("HttpContext") &&
                             !x.DeclaringType.Name.Contains("HttpRequest")))
                .ToList();
    }
}