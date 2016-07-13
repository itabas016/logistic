using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    /// <summary>
	/// Xml Utilities.
	/// </summary>
	public static class XmlUtilities
    {
        private const int SummaryLength = 300;

        /// <summary>
        /// Retrieves a child of an XmlNode and ensures it is not null.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="xpath"></param>
        /// <returns></returns>
        public static XmlNode SafeSelect(XmlNode parent, string xpath)
        {
            XmlNode node = parent.SelectSingleNode(xpath);
            if (node == null)
                throw new IntegrationException(string.Format("XmlNode not found at path {0}; {1}...", xpath, Summarize(parent.OuterXml)));

            return node;
        }

        /// <summary>
        /// Retrieves a child of an XmlNode and ensures it is not null.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="xpath"></param>
        /// <param name="namespaceManager"></param>
        /// <returns></returns>
        public static XmlNode SafeSelect(XmlNode parent, string xpath, XmlNamespaceManager namespaceManager)
        {
            XmlNode node = parent.SelectSingleNode(xpath, namespaceManager);
            if (node == null)
                throw new IntegrationException(string.Format("XmlNode not found at path {0}; {1}...", xpath, Summarize(parent.OuterXml)));

            return node;
        }

        /// <summary>
        /// Given an xpath statement, retrieves the InnerText of the XmlNode or empty string if the node is null.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="xpath"></param>
        /// <returns></returns>
        public static string SafeSelectText(XmlNode parent, string xpath)
        {
            return SafeSelectText(parent, xpath, string.Empty);
        }

        /// <summary>
        /// Given an xpath statement, retrieves the InnerText of the XmlNode or alternateValue if the node is null.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="xpath"></param>
        /// <param name="alternateValue"></param>
        /// <returns></returns>
        public static string SafeSelectText(XmlNode parent, string xpath, string alternateValue)
        {
            XmlNode node = parent.SelectSingleNode(xpath);

            // if the node does NOT exist OR if the value of the node is NULL or empty, then return the alternate value
            string value = node == null ? alternateValue : string.IsNullOrEmpty(node.InnerText) ? alternateValue : node.InnerText;

            return value;
        }

        /// <summary>
        /// Given an xpath statement, retrieves the value of the XmlNode or empty string if the node is null.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="xpath"></param>
        /// <param name="namespaceManager"></param>
        /// <returns></returns>
        public static string SafeSelectText(XmlNode parent, string xpath, XmlNamespaceManager namespaceManager)
        {
            return SafeSelectText(parent, xpath, string.Empty, namespaceManager);
        }

        /// <summary>
        /// Given an xpath statement, retrieves the InnerText of the XmlNode or alternateValue if the node is null.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="xpath"></param>
        /// <param name="alternateValue"></param>
        /// <param name="namespaceManager"></param>
        /// <returns></returns>
        public static string SafeSelectText(XmlNode parent, string xpath, string alternateValue, XmlNamespaceManager namespaceManager)
        {
            XmlNode node = parent.SelectSingleNode(xpath, namespaceManager);

            // if the node does NOT exist OR if the value of the node is NULL or empty, then return the alternate value
            string value = node == null ? alternateValue : string.IsNullOrEmpty(node.InnerText) ? alternateValue : node.InnerText;

            return value;
        }

        /// <summary>
        /// Given an xpath statement, retrieves the InnerText of the XmlNode or alternateValue if the node is null.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="nodeListOfXPathsToSearchFor"></param>
        /// <returns></returns>
        public static string SafeSelectText(XmlNode parent, XmlNodeList nodeListOfXPathsToSearchFor)
        {
            string returnValue = string.Empty;

            foreach (XmlNode nodeToSerchFor in nodeListOfXPathsToSearchFor)
            {
                string value = XmlUtilities.SafeSelectText(parent, nodeToSerchFor.InnerText);

                if (!string.IsNullOrEmpty(value))
                {
                    returnValue = value;
                    break;
                }
            }

            return returnValue;
        }


        /// <summary>
        /// Given an xpath statement, retrieves the InnerText of the XmlNode or alternateValue if the node is null.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="nodeListOfXPathsToSearchFor"></param>
        /// <param name="namespaceManager"></param>
        /// <returns></returns>
        public static string SafeSelectText(XmlNode parent, XmlNodeList nodeListOfXPathsToSearchFor, XmlNamespaceManager namespaceManager)
        {
            string returnValue = string.Empty;

            foreach (XmlNode nodeToSerchFor in nodeListOfXPathsToSearchFor)
            {
                string value = XmlUtilities.SafeSelectText(parent, nodeToSerchFor.InnerText, namespaceManager);

                if (!string.IsNullOrEmpty(value))
                {
                    returnValue = value;
                    break;
                }
            }

            return returnValue;
        }


        /// <summary>
        /// Retrieves node list from an XmlNode and ensures it is not empty.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="xpath"></param>
        public static XmlNodeList SafeSelectList(XmlNode parent, string xpath)
        {
            XmlNodeList nodes = parent.SelectNodes(xpath);
            if (nodes.Count == 0)
                throw new IntegrationException(string.Format("XmlNodeList not found at path {0}; {1}...", xpath, Summarize(parent.OuterXml)));

            return nodes;
        }

        /// <summary>
        /// Retrieves node list from an XmlNode and ensures it is not empty.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="xpath"></param>
        /// <param name="namespaceManager"></param>
        public static XmlNodeList SafeSelectList(XmlNode parent, string xpath, XmlNamespaceManager namespaceManager)
        {

            XmlNodeList nodes = parent.SelectNodes(xpath, namespaceManager);
            if (nodes.Count == 0)
                throw new IntegrationException(string.Format("XmlNodeList not found at path {0}; {1}...", xpath, Summarize(parent.OuterXml)));

            return nodes;
        }

        /// <summary>
        /// Clones an XML document.  Handy since XmlDocument.Clone returns an XmlNode.
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        public static XmlDocument Clone(XmlDocument xmlDoc)
        {
            XmlNode node = xmlDoc.Clone();
            XmlDocument document = new XmlDocument();
            document.LoadXml(node.OuterXml);
            return document;
        }

        /// <summary>
        /// Returns an attribute value (if one exists)
        /// </summary>
        /// <param name="node"></param>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        public static string SafeGetAttributeValue(XmlNode node, string attributeName)
        {
            // The left side will be evaluated first.
            if (node != null && node.Attributes != null && node.Attributes[attributeName] != null)
            {
                return node.Attributes[attributeName].Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Adds an attribute to an XmlNode
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="node"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XmlAttribute AddAttribute(XmlDocument xmlDocument, XmlNode node, string name, string value)
        {
            XmlAttribute attrib = xmlDocument.CreateAttribute(name);
            attrib.Value = value;
            node.Attributes.Append(attrib);
            return attrib;
        }

        /// <summary>
        /// Adds a child node to a parent node
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="parentNode"></param>
        /// <param name="name"></param>
        /// <param name="innerText"></param>
        /// <returns></returns>
        public static XmlNode AddNode(XmlDocument xmlDocument, XmlNode parentNode, string name, string innerText)
        {
            XmlNode childNode = AddNode(xmlDocument, parentNode, name);
            childNode.InnerText = innerText;
            return childNode;
        }

        /// <summary>
        /// Adds a child node to a parent node
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="parentNode"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static XmlNode AddNode(XmlDocument xmlDocument, XmlNode parentNode, string name)
        {
            XmlNode childNode = xmlDocument.CreateElement(name);
            parentNode.AppendChild(childNode);
            return childNode;
        }

        #region Transform
        /// <summary>
		/// Transforms an XML file.  This is done using an Xml Reader and Writer to prevent the entire file from being loaded into memory all at once.
		/// </summary>
        /// <param name="inputFilePath"></param>
		/// <param name="styleSheetUri"></param>
        /// <param name="outputFilePath"></param>
        public static void TransformFile(string inputFilePath, string styleSheetUri, string outputFilePath)
        {
            TransformFile(inputFilePath, styleSheetUri, outputFilePath, null, false, false);
        }

        /// <summary>
        /// Transforms an XML file.  This is done using an Xml Reader and Writer to prevent the entire file from being loaded into memory all at once.
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="outputFilePath"></param>
        /// <param name="isTextOutput"></param>
        public static void TransformFile(string inputFilePath, string styleSheetUri, string outputFilePath, bool isTextOutput)
        {
            TransformFile(inputFilePath, styleSheetUri, outputFilePath, null, isTextOutput, false);
        }

        /// <summary>
        /// Transforms an XML file.  This is done using an Xml Reader and Writer to prevent the entire file from being loaded into memory all at once.
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="outputFilePath"></param>
        /// <param name="isTextOutput"></param>
        /// <param name="isMultiXSLT"></param>
        public static void TransformFile(string inputFilePath, string styleSheetUri, string outputFilePath, bool isTextOutput, bool isMultiXSLT)
        {
            TransformFile(inputFilePath, styleSheetUri, outputFilePath, null, isTextOutput, isMultiXSLT);
        }
        /// <summary>
        /// Transforms an XML file.  This is done using an Xml Reader and Writer to prevent the entire file from being loaded into memory all at once.
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="outputFilePath"></param>
        /// <param name="xsltArgs"></param>
        /// <param name="isTextOutput"></param>
        /// <param name="isMultiXSLT"></param>
        public static void TransformFile(string inputFilePath, string styleSheetUri, string outputFilePath, XsltArgumentList xsltArgs, bool isTextOutput, bool isMultiXSLT)
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.CloseInput = true;
            readerSettings.IgnoreProcessingInstructions = true;
            readerSettings.IgnoreComments = true;
            readerSettings.IgnoreWhitespace = true;

            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.CloseOutput = true;

            using (XmlReader reader = XmlReader.Create(inputFilePath, readerSettings))
            using (XmlWriter writer = XmlWriter.Create(outputFilePath, writerSettings))
            {
                XmlUtilities.Transform(reader, styleSheetUri, writer, xsltArgs, isTextOutput, isMultiXSLT);
                writer.Flush();
            }
        }

        /// <summary>
        /// Transforms an XML file.  This is done using an Xml Reader and Writer to prevent the entire file from being loaded into memory all at once.
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="outputFilePath"></param>
        /// <param name="xsltArgs"></param>
        public static void TransformFile(string inputFilePath, string styleSheetUri, string outputFilePath, XsltArgumentList xsltArgs)
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.CloseInput = true;
            readerSettings.IgnoreProcessingInstructions = true;
            readerSettings.IgnoreComments = true;
            readerSettings.IgnoreWhitespace = true;
            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.CloseOutput = true;
            using (XmlReader reader = XmlReader.Create(inputFilePath, readerSettings))
            using (XmlWriter writer = XmlWriter.Create(outputFilePath, writerSettings))
            {
                XmlUtilities.Transform(reader, styleSheetUri, writer, xsltArgs, false, false);
                writer.Flush();
            }
        }

        /// <summary>
        /// Transforms a string of XML.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="styleSheetUri"></param>
        /// <returns></returns>
        public static string TransformXml(string xml, string styleSheetUri)
        {
            return TransformXml(xml, styleSheetUri, false, false);
        }


        /// <summary>
        /// Transform a string of XML with the transformer that has multiple XSLT.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="isMultiXSLT"></param>
        /// <returns></returns>
        public static string TransformXmlWithMultiXSLT(string xml, string styleSheetUri, bool isMultiXSLT)
        {
            return TransformXml(xml, styleSheetUri, false, isMultiXSLT);
        }

        /// <summary>
        /// Transforms a string of XML with the output expected to be text.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="isTextOutput">Defines Expected XSLT Output as text</param>
        /// <returns></returns>
        public static string TransformXml(string xml, string styleSheetUri, bool isTextOutput)
        {
            return TransformXml(xml, styleSheetUri, isTextOutput, false);
        }

        /// <summary>
        /// Transforms a string of XML with the output expected to be text. Transforms using XLST files which include otehr XSLT files.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="isTextOutput">Defines Expected XSLT Output as text</param>
        /// <param name="isMultiXSLT"></param>
        /// <returns></returns>
        public static string TransformXml(string xml, string styleSheetUri, bool isTextOutput, bool isMultiXSLT)
        {
            // Create the XmlReader and then a StringBuilder to hold the output.
            XmlReader reader = XmlReader.Create(new StringReader(xml));
            StringBuilder builder = new StringBuilder();
            string result;

            if (isTextOutput)
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.OmitXmlDeclaration = true;
                // For text output.
                settings.ConformanceLevel = ConformanceLevel.Fragment;

                using (XmlWriter writer = XmlWriter.Create(builder, settings))
                {
                    Transform(reader, styleSheetUri, writer, null, isTextOutput, isMultiXSLT);
                    result = builder.ToString();
                }
            }
            else
            {
                using (XmlWriter writer = XmlWriter.Create(builder))
                {
                    // Transform the XML and get the result.
                    Transform(reader, styleSheetUri, writer, null, isTextOutput, isMultiXSLT);
                    result = builder.ToString();
                }
            }

            return result;
        }
        /// <summary>
        /// Transforms XML from an XmlReader and directs the output to an XmlWriter.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="writer"></param>
        public static void Transform(XmlReader reader, string styleSheetUri, XmlWriter writer)
        {
            Transform(reader, styleSheetUri, writer, null, false, false);
        }

        /// <summary>
        /// Transforms XML from an XmlReader and directs the output to an XmlWriter.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="writer"></param>
        /// <param name="isTextOutput">Defines Expected XSLT Output as text</param>
        /// TODO: THIS REALLY SHOULD be the only Transform used which means all the old code would need to be converted
        /// to pass the extra parameter.
        public static void Transform(XmlReader reader, string styleSheetUri, XmlWriter writer, bool isTextOutput)
        {
            Transform(reader, styleSheetUri, writer, null, isTextOutput, false);
        }

        /// <summary>
        /// Transforms XML with an XMLURLResolver from an XmlReader and directs the output to an XmlWriter.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="writer"></param>
        /// <param name="isTextOutput"></param>
        /// <param name="isMultipleXSLT"></param>
        /// TODO: THIS REALLY SHOULD be the only Transform used which means all the old code would need to be converted
        /// to pass the extra parameter.
        public static void Transform(XmlReader reader, string styleSheetUri, XmlWriter writer, bool isTextOutput, bool isMultipleXSLT)
        {
            Transform(reader, styleSheetUri, writer, null, isTextOutput, isMultipleXSLT);
        }

        /// <summary>
        /// Transforms XML with an XMLURLResolver from an XmlReader and directs the output to an XmlWriter.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="styleSheetUri"></param>
        /// <param name="writer"></param>
        /// <param name="xsltArgs"></param>
        /// <param name="isTextOutput"></param>
        /// <param name="isMultipleXSLT"></param>
        /// TODO: THIS REALLY SHOULD be the only Transform used which means all the old code would need to be converted
        /// to pass the extra parameter.
        public static void Transform(XmlReader reader, string styleSheetUri, XmlWriter writer, XsltArgumentList xsltArgs, bool isTextOutput, bool isMultipleXSLT)
        {
            // NOTE: The below transform always outputs UTF-16 encoding,
            // even when the XSLT says not to, and also emits an XML declaration
            // even when you use <xsl:output omit-xml-declaration="yes"/>.
            // So once we figure out how to obey the XSLT correctly we can remove the
            // below hard-coded ProcessingInstruction.

            // Check to see if the encoding needs to be set. Set the encoding.
            if (!isTextOutput)
            {
                writer.WriteProcessingInstruction("xml", "version=\"1.0\" encoding=\"utf-8\"");
            }
            else
            {
            }

            XslCompiledTransform xslCompiledTransform = XsltUtilities.GetCompiledTransform(styleSheetUri);

            // Perform the transformation.
            xslCompiledTransform.Transform(reader, xsltArgs, writer);
        }
        #endregion

        /// <summary>
		/// Updates the text of a child node.  Creates the node if it does not exist.
		/// </summary>
		/// <param name="parentNode"></param>
		/// <param name="childName"></param>
		/// <param name="textValue"></param>
		public static void UpdateChildText(XmlNode parentNode, string childName, string textValue)
        {
            // Find the child.
            XmlNode childNode = parentNode.SelectSingleNode(childName);

            if (childNode != null)
            {
                // Update the child.
                childNode.InnerText = textValue;
            }
            else
            {
                // Add the child.
                childNode = parentNode.OwnerDocument.CreateNode(XmlNodeType.Element, childName, string.Empty);
                childNode.InnerText = textValue;
                parentNode.AppendChild(childNode);
            }
        }

        /// <summary>
        /// Streams the current node from an XmlReader to an XmlWriter.
        /// NOTE: Found on Mark Fussell's WebLog (http://blogs.msdn.com/mfussell/archive/2005/02/12/371546.aspx).
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="writer"></param>
        public static void WriteShallowNode(XmlReader reader, XmlWriter writer)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            if (writer == null)
                throw new ArgumentNullException("writer");

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    writer.WriteAttributes(reader, true);
                    if (reader.IsEmptyElement)
                        writer.WriteEndElement();
                    break;

                case XmlNodeType.Text:
                    writer.WriteString(reader.Value);
                    break;

                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    writer.WriteWhitespace(reader.Value);
                    break;

                case XmlNodeType.CDATA:
                    writer.WriteCData(reader.Value);
                    break;

                case XmlNodeType.EntityReference:
                    writer.WriteEntityRef(reader.Name);
                    break;

                case XmlNodeType.XmlDeclaration:
                case XmlNodeType.ProcessingInstruction:
                    writer.WriteProcessingInstruction(reader.Name, reader.Value);
                    break;

                case XmlNodeType.DocumentType:
                    writer.WriteDocType(reader.Name, reader.GetAttribute("PUBLIC"), reader.GetAttribute("SYSTEM"), reader.Value);
                    break;

                case XmlNodeType.Comment:
                    writer.WriteComment(reader.Value);
                    break;

                case XmlNodeType.EndElement:
                    writer.WriteFullEndElement();
                    break;
            }
        }

        /// <summary>
        /// Traverses xml using XmlReader until given element name is found 
        /// and returns element's inner xml value.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="elementName"></param>
        /// <returns></returns>
        public static string SafeReadToFollowing(XmlReader reader, string elementName)
        {
            bool found = true;
            string readValue = string.Empty;
            if (reader.Name != elementName)
            {
                found = reader.ReadToFollowing(elementName);
            }
            if (found && !reader.IsEmptyElement)
            {
                readValue = (reader.ReadInnerXml());
            }
            return readValue;
        }

        /// <summary>
        /// Traverses xml using XmlReader until given descendant's element name is found 
        /// and returns element's inner xml value.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="elementName"></param>
        /// <returns></returns>
        public static string SafeReadToDescendant(XmlReader reader, string elementName)
        {
            bool found = true;
            string readValue = string.Empty;
            if (reader.Name != elementName)
            {
                found = reader.ReadToDescendant(elementName);
            }
            if (found && !reader.IsEmptyElement)
            {
                readValue = (reader.ReadInnerXml());
            }
            return readValue;
        }

        public static string SafeGetNodeValue(XPathNavigator xPathNav, string xPath)
        {
            string nodeValue = string.Empty;
            XPathNavigator xSelectPathNav = xPathNav.SelectSingleNode(xPath);
            if (xSelectPathNav != null)
            {
                nodeValue = xSelectPathNav.Value;
            }
            return nodeValue;
        }

        private static string Summarize(string value)
        {
            return ValidationUtilities.SafeSubstring(value, 0, SummaryLength);
        }
        public static string Evaluate(string xpath, XPathNavigator navigator, IXmlNamespaceResolver resolver)
        {
            string value = navigator.Evaluate(xpath, resolver).ToString();
            if (value.Trim() == "MS.Internal.Xml.XPath.XPathSelectionIterator")
            {
                XPathNodeIterator NodeIter = navigator.Select(xpath, resolver);
                value = "";
                while (NodeIter.MoveNext())
                    value += NodeIter.Current.Value;
            }

            return value;
        }

        public static XmlNamespaceManager LoadXmlNamespaceManager(XmlDocument xDoc)
        {
            XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xDoc.NameTable);
            AddNamespaces(ref xmlNamespaceManager);
            return xmlNamespaceManager;
        }

        private static void AddNamespaces(ref XmlNamespaceManager ns)
        {
            ns.AddNamespace("C", "http://ibs.entriq.net/Customers");
            ns.AddNamespace("AM", "http://ibs.entriq.net/AgreementManagement");
            ns.AddNamespace("D", "http://ibs.entriq.net/Devices");
            ns.AddNamespace("DC", "http://ibs.entriq.net/DeviceCollection");
            ns.AddNamespace("H", "http://ibs.entriq.net/Core");
            ns.AddNamespace("PC", "http://ibs.entriq.net/ProductCatalog");
            ns.AddNamespace("P", "http://ibs.entriq.net/Provisioning");
            ns.AddNamespace("I", "http://www.w3.org/2001/XMLSchema-instance");
            ns.AddNamespace("E", "http://ibs.entriq.net/OrderableEvent");
            ns.AddNamespace("L", "http://ibs.entriq.net/Letters");
            ns.AddNamespace("SC", "http://ibs.entriq.net/SharedContracts");
            ns.AddNamespace("F", "http://ibs.entriq.net/Finance");
            ns.AddNamespace("Q", "http://ibs.entriq.net/Query");
        }

        /// <summary>
        /// Searches an XML string using a regular expression and replaces any occurrances of an XML declaration with an empty string.
        /// </summary>
        /// <param name="xml">XML string to parse</param>
        /// <returns>XML string with no XML declaration</returns>
        public static string RemoveXmlDeclaration(string xml)
        {
            var regex = new Regex(@"<\?xml.*?\?>");
            return regex.Replace(xml, string.Empty);
        }

        /// <summary>
        /// Renames the XmlNode
        /// </summary>
        /// <param name="node"></param>
        /// <param name="namespaceURI"></param>
        /// <param name="qualifiedName"></param>
        /// <returns></returns>
        public static XmlNode RenameNode(XmlNode node, string namespaceURI, string qualifiedName)
        {
            if (node.NodeType == XmlNodeType.Element)
            {
                XmlElement oldElement = (XmlElement)node;

                XmlElement newElement =
                node.OwnerDocument.CreateElement(qualifiedName, namespaceURI);

                while (oldElement.HasAttributes)
                    newElement.SetAttributeNode(oldElement.RemoveAttributeNode(oldElement.Attributes[0]));

                while (oldElement.HasChildNodes)
                    newElement.AppendChild(oldElement.FirstChild);

                if (oldElement.ParentNode != null)
                    oldElement.ParentNode.ReplaceChild(newElement, oldElement);

                return newElement;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Converts a given string to XmlNode
        /// </summary>
        /// <param name="strXml"></param>
        /// <returns></returns>
        public static XmlNode StringToXmlNode(string strXml)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                if (!string.IsNullOrEmpty(strXml))
                {
                    doc.LoadXml(strXml);
                    return doc.DocumentElement;
                }
                else
                    return doc.DocumentElement;
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Error while converting String to XML. Error Message : {0}", ex.Message));
            }
        }
    }
}
