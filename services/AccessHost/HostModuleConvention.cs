using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace AccessHost;

internal sealed class HostModuleConvention : IApplicationModelConvention
{
    private readonly IReadOnlyDictionary<Assembly, HostModuleDescriptor> _modules;

    public HostModuleConvention(IEnumerable<HostModuleDescriptor> modules)
    {
        _modules = modules.ToDictionary(module => module.Assembly);
    }

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            if (!_modules.TryGetValue(controller.ControllerType.Assembly, out var module))
            {
                continue;
            }

            controller.ApiExplorer.GroupName = module.GroupName;

            foreach (var selector in controller.Selectors)
            {
                if (selector.AttributeRouteModel is null)
                {
                    selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(module.RoutePrefix));
                    continue;
                }

                selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(
                    new AttributeRouteModel(new RouteAttribute(module.RoutePrefix)),
                    selector.AttributeRouteModel);
            }
        }
    }
}

internal sealed record HostModuleDescriptor(Assembly Assembly, string RoutePrefix, string GroupName);
