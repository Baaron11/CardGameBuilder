using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardGameBuilder.Modding
{
    /// <summary>
    /// Runtime rule graph editor UI.
    /// Allows users to create, edit, and connect rule nodes visually.
    /// Uses Unity UI Canvas system for node-based editing.
    /// </summary>
    public class RuleGraphEditor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform graphCanvas;
        [SerializeField] private GameObject nodeTemplate;
        [SerializeField] private LineRenderer linkLineTemplate;
        [SerializeField] private ScrollRect scrollRect;

        [Header("UI Panels")]
        [SerializeField] private GameObject nodeCreationPanel;
        [SerializeField] private TMP_Dropdown nodeTypeDropdown;
        [SerializeField] private TMP_Dropdown nodeSubTypeDropdown;
        [SerializeField] private Button createNodeButton;
        [SerializeField] private GameObject nodePropertiesPanel;
        [SerializeField] private Transform propertiesContainer;

        [Header("Prefabs")]
        [SerializeField] private GameObject parameterInputPrefab;

        private CustomGameDefinition currentDefinition;
        private Dictionary<string, GameObject> nodeVisuals = new Dictionary<string, GameObject>();
        private Dictionary<string, LineRenderer> linkVisuals = new Dictionary<string, LineRenderer>();
        private RuleNode selectedNode;
        private RuleNode linkSourceNode;
        private bool isDraggingNode = false;
        private Vector2 dragOffset;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            if (createNodeButton != null)
            {
                createNodeButton.onClick.AddListener(OnCreateNodeClicked);
            }

            if (nodeTypeDropdown != null)
            {
                nodeTypeDropdown.ClearOptions();
                nodeTypeDropdown.AddOptions(new List<string> { "Event", "Condition", "Action" });
                nodeTypeDropdown.onValueChanged.AddListener(OnNodeTypeChanged);
            }

            UpdateSubTypeDropdown(RuleNodeType.Event);
        }

        public void Initialize(CustomGameDefinition definition)
        {
            currentDefinition = definition;
            if (currentDefinition.rules == null)
            {
                currentDefinition.rules = new RuleGraph();
            }

            RefreshGraph();
        }

        public void RefreshGraph()
        {
            ClearVisuals();

            if (currentDefinition == null || currentDefinition.rules == null)
                return;

            // Create visual nodes
            foreach (var node in currentDefinition.rules.nodes)
            {
                CreateNodeVisual(node);
            }

            // Create visual links
            foreach (var link in currentDefinition.rules.links)
            {
                CreateLinkVisual(link);
            }
        }

        private void ClearVisuals()
        {
            foreach (var visual in nodeVisuals.Values)
            {
                if (visual != null)
                    Destroy(visual);
            }
            nodeVisuals.Clear();

            foreach (var line in linkVisuals.Values)
            {
                if (line != null)
                    Destroy(line.gameObject);
            }
            linkVisuals.Clear();
        }

        private void OnNodeTypeChanged(int index)
        {
            RuleNodeType nodeType = (RuleNodeType)index;
            UpdateSubTypeDropdown(nodeType);
        }

        private void UpdateSubTypeDropdown(RuleNodeType nodeType)
        {
            if (nodeSubTypeDropdown == null)
                return;

            nodeSubTypeDropdown.ClearOptions();

            List<string> options = new List<string>();
            switch (nodeType)
            {
                case RuleNodeType.Event:
                    options.AddRange(Enum.GetNames(typeof(EventNodeSubType)));
                    break;
                case RuleNodeType.Condition:
                    options.AddRange(Enum.GetNames(typeof(ConditionNodeSubType)));
                    break;
                case RuleNodeType.Action:
                    options.AddRange(Enum.GetNames(typeof(ActionNodeSubType)));
                    break;
            }

            nodeSubTypeDropdown.AddOptions(options);
        }

        public void OnCreateNodeClicked()
        {
            if (currentDefinition == null)
                return;

            RuleNodeType nodeType = (RuleNodeType)nodeTypeDropdown.value;
            string subType = nodeSubTypeDropdown.options[nodeSubTypeDropdown.value].text;

            RuleNode newNode = new RuleNode
            {
                id = Guid.NewGuid().ToString(),
                name = $"{subType}",
                nodeType = nodeType,
                subType = subType,
                position = new Vector2(100, 100)
            };

            // Add default parameters based on node type
            AddDefaultParameters(newNode);

            currentDefinition.rules.nodes.Add(newNode);
            CreateNodeVisual(newNode);

            Debug.Log($"Created node: {newNode.name} ({newNode.nodeType})");
        }

        private void AddDefaultParameters(RuleNode node)
        {
            switch (node.nodeType)
            {
                case RuleNodeType.Event:
                    // Events don't need parameters
                    break;

                case RuleNodeType.Condition:
                    switch (node.subType)
                    {
                        case "CompareCardValue":
                            node.SetParameter("operator", ">");
                            node.SetParameter("value", "5");
                            node.outputPortIds = new List<string> { "true", "false" };
                            break;
                        case "CheckHandEmpty":
                            node.SetParameter("playerIndex", "0");
                            node.outputPortIds = new List<string> { "true", "false" };
                            break;
                        case "CheckHandCount":
                            node.SetParameter("operator", ">");
                            node.SetParameter("count", "3");
                            node.outputPortIds = new List<string> { "true", "false" };
                            break;
                        case "CheckDeckEmpty":
                            node.SetParameter("deckIndex", "0");
                            node.outputPortIds = new List<string> { "true", "false" };
                            break;
                        case "CheckScore":
                            node.SetParameter("playerIndex", "0");
                            node.SetParameter("operator", ">=");
                            node.SetParameter("value", "10");
                            node.outputPortIds = new List<string> { "true", "false" };
                            break;
                        default:
                            node.outputPortIds = new List<string> { "true", "false" };
                            break;
                    }
                    break;

                case RuleNodeType.Action:
                    switch (node.subType)
                    {
                        case "DrawCard":
                            node.SetParameter("playerIndex", "current");
                            node.SetParameter("count", "1");
                            node.SetParameter("deckIndex", "0");
                            break;
                        case "PlayCard":
                            node.SetParameter("playerIndex", "current");
                            node.SetParameter("cardIndex", "0");
                            break;
                        case "AddScore":
                            node.SetParameter("playerIndex", "current");
                            node.SetParameter("points", "1");
                            break;
                        case "SubtractScore":
                            node.SetParameter("playerIndex", "current");
                            node.SetParameter("points", "1");
                            break;
                        case "SetScore":
                            node.SetParameter("playerIndex", "current");
                            node.SetParameter("score", "0");
                            break;
                        case "ShowMessage":
                            node.SetParameter("message", "Game event occurred");
                            break;
                        case "EndGame":
                            node.SetParameter("winnerIndex", "current");
                            break;
                        default:
                            break;
                    }
                    break;
            }
        }

        private GameObject CreateNodeVisual(RuleNode node)
        {
            if (nodeTemplate == null || graphCanvas == null)
            {
                Debug.LogError("Node template or graph canvas not assigned!");
                return null;
            }

            GameObject nodeObj = Instantiate(nodeTemplate, graphCanvas);
            RectTransform rectTransform = nodeObj.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = node.position;

            // Set node color based on type
            Image bgImage = nodeObj.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = GetNodeColor(node.nodeType);
            }

            // Set node text
            TextMeshProUGUI nodeText = nodeObj.GetComponentInChildren<TextMeshProUGUI>();
            if (nodeText != null)
            {
                nodeText.text = node.name;
            }

            // Add drag functionality
            NodeDragHandler dragHandler = nodeObj.AddComponent<NodeDragHandler>();
            dragHandler.Initialize(node, this);

            // Add click functionality
            Button nodeButton = nodeObj.GetComponent<Button>();
            if (nodeButton == null)
            {
                nodeButton = nodeObj.AddComponent<Button>();
            }
            nodeButton.onClick.AddListener(() => OnNodeClicked(node));

            nodeVisuals[node.id] = nodeObj;
            return nodeObj;
        }

        private Color GetNodeColor(RuleNodeType nodeType)
        {
            switch (nodeType)
            {
                case RuleNodeType.Event:
                    return new Color(0.3f, 0.5f, 1f); // Blue
                case RuleNodeType.Condition:
                    return new Color(1f, 0.6f, 0.2f); // Orange
                case RuleNodeType.Action:
                    return new Color(0.3f, 0.8f, 0.3f); // Green
                default:
                    return Color.gray;
            }
        }

        private void CreateLinkVisual(RuleLink link)
        {
            if (linkLineTemplate == null)
            {
                Debug.LogError("Link line template not assigned!");
                return;
            }

            if (!nodeVisuals.ContainsKey(link.fromNodeId) || !nodeVisuals.ContainsKey(link.toNodeId))
            {
                Debug.LogWarning($"Cannot create link visual - missing node visuals");
                return;
            }

            LineRenderer line = Instantiate(linkLineTemplate, graphCanvas);
            line.positionCount = 2;

            UpdateLinkVisual(link, line);
            linkVisuals[link.id] = line;
        }

        private void UpdateLinkVisual(RuleLink link, LineRenderer line)
        {
            if (!nodeVisuals.ContainsKey(link.fromNodeId) || !nodeVisuals.ContainsKey(link.toNodeId))
                return;

            Vector3 fromPos = nodeVisuals[link.fromNodeId].transform.position;
            Vector3 toPos = nodeVisuals[link.toNodeId].transform.position;

            line.SetPosition(0, fromPos);
            line.SetPosition(1, toPos);
        }

        public void OnNodeClicked(RuleNode node)
        {
            selectedNode = node;
            ShowNodeProperties(node);
        }

        private void ShowNodeProperties(RuleNode node)
        {
            if (nodePropertiesPanel == null || propertiesContainer == null)
                return;

            nodePropertiesPanel.SetActive(true);

            // Clear existing properties
            foreach (Transform child in propertiesContainer)
            {
                Destroy(child.gameObject);
            }

            // Add name field
            CreatePropertyField("Name", node.name, (value) => {
                node.name = value;
                RefreshNodeVisual(node);
            });

            // Add parameter fields
            foreach (var param in node.parameters)
            {
                string paramKey = param.key;
                CreatePropertyField(param.key, param.value, (value) => {
                    node.SetParameter(paramKey, value);
                });
            }

            // Add delete button
            GameObject deleteButton = new GameObject("DeleteButton");
            deleteButton.transform.SetParent(propertiesContainer, false);
            Button btn = deleteButton.AddComponent<Button>();
            TextMeshProUGUI btnText = deleteButton.AddComponent<TextMeshProUGUI>();
            btnText.text = "Delete Node";
            btn.onClick.AddListener(() => DeleteNode(node));
        }

        private void CreatePropertyField(string label, string value, Action<string> onValueChanged)
        {
            if (parameterInputPrefab == null || propertiesContainer == null)
                return;

            GameObject fieldObj = Instantiate(parameterInputPrefab, propertiesContainer);

            TextMeshProUGUI labelText = fieldObj.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (labelText != null)
            {
                labelText.text = label;
            }

            TMP_InputField inputField = fieldObj.transform.Find("InputField")?.GetComponent<TMP_InputField>();
            if (inputField != null)
            {
                inputField.text = value;
                inputField.onEndEdit.AddListener(onValueChanged);
            }
        }

        private void RefreshNodeVisual(RuleNode node)
        {
            if (nodeVisuals.ContainsKey(node.id))
            {
                TextMeshProUGUI nodeText = nodeVisuals[node.id].GetComponentInChildren<TextMeshProUGUI>();
                if (nodeText != null)
                {
                    nodeText.text = node.name;
                }
            }
        }

        public void DeleteNode(RuleNode node)
        {
            // Remove links connected to this node
            currentDefinition.rules.links.RemoveAll(link =>
                link.fromNodeId == node.id || link.toNodeId == node.id);

            // Remove node
            currentDefinition.rules.nodes.Remove(node);

            RefreshGraph();
            nodePropertiesPanel?.SetActive(false);
        }

        public void CreateLink(RuleNode fromNode, RuleNode toNode)
        {
            RuleLink newLink = new RuleLink
            {
                fromNodeId = fromNode.id,
                toNodeId = toNode.id
            };

            currentDefinition.rules.links.Add(newLink);
            CreateLinkVisual(newLink);

            Debug.Log($"Created link: {fromNode.name} -> {toNode.name}");
        }

        public void SaveGraph()
        {
            if (currentDefinition != null)
            {
                string validationError;
                if (currentDefinition.rules.Validate(out validationError))
                {
                    Debug.Log("Rule graph saved successfully!");
                }
                else
                {
                    Debug.LogWarning($"Rule graph validation warning: {validationError}");
                }
            }
        }

        public void OnUpdateNodePosition(RuleNode node, Vector2 newPosition)
        {
            node.position = newPosition;

            // Update all connected links
            foreach (var link in currentDefinition.rules.links)
            {
                if (link.fromNodeId == node.id || link.toNodeId == node.id)
                {
                    if (linkVisuals.ContainsKey(link.id))
                    {
                        UpdateLinkVisual(link, linkVisuals[link.id]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles dragging of nodes in the graph editor.
    /// </summary>
    public class NodeDragHandler : MonoBehaviour, UnityEngine.EventSystems.IDragHandler,
        UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IEndDragHandler
    {
        private RuleNode node;
        private RuleGraphEditor editor;
        private RectTransform rectTransform;
        private Canvas canvas;

        public void Initialize(RuleNode node, RuleGraphEditor editor)
        {
            this.node = node;
            this.editor = editor;
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Start dragging
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (rectTransform != null && canvas != null)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out localPoint
                );

                rectTransform.anchoredPosition = localPoint;
            }
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (editor != null && rectTransform != null)
            {
                editor.OnUpdateNodePosition(node, rectTransform.anchoredPosition);
            }
        }
    }
}
