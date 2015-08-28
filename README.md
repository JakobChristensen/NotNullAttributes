# Not Null Attributes
Visual Studio Analyzer for highlighting propeties, and methods without NotNull or CanBeNull attributes.

This analyzer will add a green squiggly to any method parameters, method returns or properties that are not marked
with a NotNullAttribute or CanBeNullAttribute. These attributes can be declared in any namespace.

The attributes can be used with ReSharper Value Analysis to highlight possible null references.

## Say "no" to "null"
A great article on www.elegantcode.com that explains why it is better not to pass null values as parameters.

http://elegantcode.com/2010/05/01/say-no-to-null/
