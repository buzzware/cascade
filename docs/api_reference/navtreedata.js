/*
 @licstart  The following is the entire license notice for the JavaScript code in this file.

 The MIT License (MIT)

 Copyright (C) 1997-2020 by Dimitri van Heesch

 Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 and associated documentation files (the "Software"), to deal in the Software without restriction,
 including without limitation the rights to use, copy, modify, merge, publish, distribute,
 sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all copies or
 substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

 @licend  The above is the entire license notice for the JavaScript code in this file
*/
var NAVTREE =
[
  [ "Cascade", "index.html", [
    [ "Cascade Data Layer: A Framework for Mobile App Data Flow", "index.html", null ],
    [ "Association Helper Methods", "association_helpers.html", null ],
    [ "Associations", "associations.html", null ],
    [ "How Associations Work", "how_associations_work.html", null ],
    [ "Associations Maintained through Create, Update and Replace Operations", "maintained_associations.html", null ],
    [ "Populating Associations", "populating.html", null ],
    [ "Glossary", "glossary.html", null ],
    [ "Blobs In Depth", "blobs_in_depth.html", null ],
    [ "Collections in Depth", "collections_in_depth.html", null ],
    [ "Freshness, Fallback and Time", "freshness_and_fallback.html", null ],
    [ "Implementing a Custom ICascadeOrigin for Your Server", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html", [
      [ "Introduction", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md32", null ],
      [ "Why CascadeOrigin is Required", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md33", null ],
      [ "Minimum Contract: ICascadeOrigin", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md34", null ],
      [ "Recommended Implementation Approach", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md35", null ],
      [ "Implementation Guide", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md36", [
        [ "1. Basic Origin Structure", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md37", null ],
        [ "2. Basic Class Origin Structure", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md38", null ],
        [ "2. Implementing Core Methods", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md39", null ],
        [ "3. Implementing Blob Handling", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md40", null ]
      ] ],
      [ "Key Considerations", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md41", [
        [ "Authentication", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md42", null ],
        [ "Exception Handling", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md43", null ],
        [ "Serialization", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md44", null ],
        [ "Online/Offline Behavior", "md_api__docs_2using__cascade_2in__depth_2implementing__origin.html#autotoc_md45", null ]
      ] ]
    ] ],
    [ "Pagination with CascadePaginator", "pagination.html", null ],
    [ "Queries In Depth", "queries_in_depth.html", null ],
    [ "SuperModel In Depth", "supermodel.html", null ],
    [ "Binary Blob Handling", "binary_blob_handling.html", null ],
    [ "Creating Models", "creating_models.html", null ],
    [ "Defining Models", "defining_models.html", null ],
    [ "Deleting Models", "deleting_models.html", null ],
    [ "Design Values and Constraints", "values_and_constraints.html", null ],
    [ "Getting Models", "getting_models.html", null ],
    [ "Simple Querying with Cascade", "queries.html", null ],
    [ "Updating Models", "updating_models.html", null ],
    [ "RequestVerb", "namespace_buzzware_1_1_cascade.html#a4789189aff5514ed7b15f4aa029bdc44", null ],
    [ "SequenceType", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706", [
      [ "Invalid", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a4bbb8f967da6d1a610596d7257179c2b", null ],
      [ "String", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a27118326006d3829667a400ad23d5d98", null ],
      [ "Array", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a4410ec34d9e6c1a68100ca0ce033fb17", null ],
      [ "BitArray", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a28a78c8063c0ab41f80ce56bc51ebb1f", null ],
      [ "ArrayList", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a57a97a39435cfdfed96e03f2a3bc27ce", null ],
      [ "Queue", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a722ad2d05ecf4868b00c5484b82fd808", null ],
      [ "Stack", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a2187e1021a911b3807cc1bea2eb1a9ca", null ],
      [ "Hashtable", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706acabc0b3c65c3d25adfb69cc0517e5b3e", null ],
      [ "SortedList", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a33a04d438160a83ab3442a81bd800e01", null ],
      [ "Dictionary", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a3beb75d1563ebc22253341be4ce57f44", null ],
      [ "ListDictionary", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a07d049b8e0be68203ab57170c4c8e4da", null ],
      [ "IList", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706aad7cce600f0e290b37b99b9d8c11529b", null ],
      [ "ICollection", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a181a817e86063ab017b19cb85945f06e", null ],
      [ "IDictionary", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a57cf0c81ca7a8c21feadac6c8438bffd", null ],
      [ "IEnumerable", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a9e0dd4cd0a082d60acd7c2556faa7df7", null ],
      [ "Custom", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a90589c47f06eb971d548591f23c285af", null ],
      [ "GenericList", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a5d4658b52f0612aa6f15c57ea15ab9e5", null ],
      [ "GenericLinkedList", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706ae08183bd1c4d2fb699b9c8a63a3ecc9d", null ],
      [ "GenericCollection", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a0dc166715553d8afd6a7b0cff910d952", null ],
      [ "GenericQueue", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a0e53cfc5a4d709ca4253884f679825bf", null ],
      [ "GenericStack", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a1f309528b5998261f4e10313abedcee5", null ],
      [ "GenericHashSet", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a387ffc720e18157a95225403f6622699", null ],
      [ "GenericSortedList", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706ab7147ae0f0f3e8735032968c51ae2e4f", null ],
      [ "GenericDictionary", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a4cdef9bd5ce42558a0d17bb42bbb2c12", null ],
      [ "GenericSortedDictionary", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706af502047efdaebc0e311247f5df025c2f", null ],
      [ "GenericBlockingCollection", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a7c75d52c835c5f269fe525ce0ce2dfc1", null ],
      [ "GenericConcurrentDictionary", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a8f4cb8d3cbb36821733fe7fb175f10ef", null ],
      [ "GenericConcurrentBag", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a09f12d3799a12de48cdad45cf6b45704", null ],
      [ "GenericIList", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706aa7eb7eda4a129ba8f2dfc6aba5272f07", null ],
      [ "GenericICollection", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a5030afa59b6cb5e49b32e2b9b23393de", null ],
      [ "GenericIEnumerable", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a4669d3dc96106b31f67a9071c77f953f", null ],
      [ "GenericIDictionary", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a52af0ea7ca004faea57d3960ee09fe48", null ],
      [ "GenericICollectionKeyValue", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a74c77498fd2c6f310e62fd59f8b33b34", null ],
      [ "GenericIEnumerableKeyValue", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706a83fc31b77dd065dd58659d6da2de8217", null ],
      [ "GenericCustom", "namespace_easy_1_1_common_1_1_extensions.html#ae398b2c73642caabffdcc43c8591e706ac921bce82c8ebc50cfb501f3a4e625f4", null ]
    ] ],
    [ "Classes", "annotated.html", [
      [ "Class List", "annotated.html", "annotated_dup" ],
      [ "Class Index", "classes.html", null ],
      [ "Class Hierarchy", "hierarchy.html", "hierarchy" ],
      [ "Class Members", "functions.html", [
        [ "All", "functions.html", "functions_dup" ],
        [ "Functions", "functions_func.html", "functions_func" ],
        [ "Variables", "functions_vars.html", null ],
        [ "Properties", "functions_prop.html", null ],
        [ "Events", "functions_evnt.html", null ]
      ] ]
    ] ]
  ] ]
];

var NAVTREEINDEX =
[
"annotated.html",
"class_buzzware_1_1_cascade_1_1_from_property_attribute.html#a90c4704c8b67be8b50a6331ecf195be9",
"interface_buzzware_1_1_cascade_1_1_i_model_class_cache.html#a6eb25cd59d345166cd01bd519d46a9db"
];

const SYNCONMSG = 'click to disable panel synchronization';
const SYNCOFFMSG = 'click to enable panel synchronization';
const LISTOFALLMEMBERS = 'List of all members';