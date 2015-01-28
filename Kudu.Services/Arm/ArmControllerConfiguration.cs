using System;
using System.Web.Http.Controllers;

namespace Kudu.Services.Arm
{
    /// <summary>
    /// Use this attribute on controllers that support the ARM api.
    /// In these controllers, for each method that serves a PUT request you'll need to also implement
    /// a method with the same name + an "Arm" postfix and the input will have an Arm envelope.
    /// 
    /// For example if you have the method:
    /// public void PutItem(Item item)
    /// 
    /// You'll need to add:
    /// public void PutItemArm(ArmEntry&lt;Item&gt; armItem)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ArmControllerConfiguration : Attribute, IControllerConfiguration
    {
        public void Initialize(HttpControllerSettings controllerSettings, HttpControllerDescriptor controllerDescriptor)
        {
            controllerSettings.Services.Replace(typeof(IHttpActionSelector), new ArmActionSelector());
        }
    }
}
