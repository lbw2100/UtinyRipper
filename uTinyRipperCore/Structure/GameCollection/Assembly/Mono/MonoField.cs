using Mono.Cecil;
using uTinyRipper.Converters.Script;
using uTinyRipper.Converters.Script.Mono;

namespace uTinyRipper.Game.Assembly.Mono
{
#warning TODO: move to MonoType
	public static class MonoField
	{
		public static bool IsSerializableModifier(FieldDefinition field)
		{
			if (field.HasConstant)
			{
				return false;
			}
			if (field.IsStatic)
			{
				return false;
			}
			if (field.IsInitOnly)
			{
				return false;
			}
			if (IsCompilerGenerated(field))
			{
				return false;
			}

			if (field.IsPublic)
			{
				if (field.IsNotSerialized)
				{
					return false;
				}
				return true;
			}
			return HasSerializeFieldAttribute(field);
		}

		public static bool IsSerializable(in MonoFieldContext context)
		{
			if (IsSerializableModifier(context.Definition))
			{
				return IsFieldTypeSerializable(context);
			}
			return false;
		}

		public static bool IsFieldTypeSerializable(in MonoFieldContext context)
		{
			TypeReference fieldType = context.ElementType;

			// if it's generic parameter then get its real type
			if (fieldType.IsGenericParameter)
			{
				GenericParameter parameter = (GenericParameter)fieldType;
				fieldType = context.Arguments[parameter];
			}

			if (fieldType.IsArray)
			{
				ArrayType array = (ArrayType)fieldType;
				// one dimention array only
				if (!array.IsVector)
				{
					return false;
				}

				// if it's generic parameter then get its real type
				TypeReference elementType = array.ElementType;
				if (elementType.IsGenericParameter)
				{
					GenericParameter parameter = (GenericParameter)elementType;
					elementType = context.Arguments[parameter];
				}

				// array of arrays isn't serializable
				if (elementType.IsArray)
				{
					return false;
				}
				// array of serializable generics isn't serializable
				if (MonoType.IsSerializableGeneric(elementType))
				{
					return false;
				}
				// check if array element is serializable
				MonoFieldContext elementScope = new MonoFieldContext(context, elementType, true);
				return IsFieldTypeSerializable(elementScope);
			}

			if (MonoType.IsList(fieldType))
			{
				// list is serialized same way as array, so check its argument
				GenericInstanceType list = (GenericInstanceType)fieldType;
				TypeReference listElement = list.GenericArguments[0];

				// if it's generic parameter then get its real type
				if (listElement.IsGenericParameter)
				{
					GenericParameter parameter = (GenericParameter)listElement;
					listElement = context.Arguments[parameter];
				}

				// list of arrays isn't serializable
				if (listElement.IsArray)
				{
					return false;
				}
				// list of buildin generics isn't serializable
				if (MonoType.IsBuiltinGeneric(listElement))
				{
					return false;
				}
				// check if list element is serializable
				MonoFieldContext elementScope = new MonoFieldContext(context, listElement, true);
				return IsFieldTypeSerializable(elementScope);
			}

			if (MonoUtils.IsSerializablePrimitive(fieldType))
			{
				return true;
			}
			if (MonoType.IsObject(fieldType))
			{
				return false;
			}

			if (MonoType.IsEngineStruct(fieldType))
			{
				return true;
			}
			if (fieldType.IsGenericInstance)
			{
				// even monobehaviour derived generic instances aren't serialiable
				return MonoType.IsSerializableGeneric(fieldType);
			}
			if (MonoType.IsMonoDerived(fieldType))
			{
				if (fieldType.ContainsGenericParameter)
				{
					return false;
				}
				return true;
			}

			if (IsRecursive(context.DeclaringType, fieldType))
			{
				return context.IsArray;
			}

			TypeDefinition definition = fieldType.Resolve();
			if (definition.IsInterface)
			{
				return false;
			}
			if (definition.IsAbstract)
			{
				return false;
			}
			if (MonoType.IsCompilerGenerated(definition))
			{
				return false;
			}
			if (definition.IsEnum)
			{
				return true;
			}
			if (definition.IsSerializable)
			{
				if (ScriptExportManager.IsFrameworkLibrary(ScriptExportMonoType.GetModuleName(definition)))
				{
					return false;
				}
				if (definition.IsValueType && !MonoType.IsStructSerializable(context.Version))
				{
					return false;
				}
				return true;
			}

			return false;
		}

		public static bool IsRecursive(TypeReference declaringType, TypeReference fieldType)
		{
			// "built in" primitive .NET types are placed into itself... it is so stupid
			// field.FieldType.IsPrimitive || MonoType.IsString(field.FieldType) || MonoType.IsEnginePointer(field.FieldType) => return false
			if (MonoType.IsDelegate(fieldType))
			{
				return false;
			}
			if (declaringType == fieldType)
			{
				return true;
			}
			return false;
		}

		public static bool IsCompilerGenerated(FieldDefinition field)
		{
			foreach (CustomAttribute attribute in field.CustomAttributes)
			{
				TypeReference type = attribute.AttributeType;
				if (SerializableField.IsCompilerGeneratedAttrribute(type.Namespace, type.Name))
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsSerializableArray(TypeReference type)
		{
			return type.IsArray || MonoType.IsList(type);
		}

		private static bool HasSerializeFieldAttribute(FieldDefinition field)
		{
			foreach (CustomAttribute attribute in field.CustomAttributes)
			{
				TypeReference type = attribute.AttributeType;
				if (SerializableField.IsSerializeFieldAttrribute(type.Namespace, type.Name))
				{
					return true;
				}
			}
			return false;
		}
	}
}
