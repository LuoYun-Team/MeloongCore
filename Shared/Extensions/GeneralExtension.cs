namespace MeloongCore.Extensions;
public static class GeneralExtension {

    /// <summary>
    /// 判断对象是否为指定泛型类型的实例。
    /// </summary>
    public static bool IsGenericInstanceOf(this object? instance, Type genericTypeDefinition) {
        if (instance is null) return false;
        for (var type = instance.GetType(); type is not null; type = type.BaseType) {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition) return true;
        }
        return false;
    }

}
