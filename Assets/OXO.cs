using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class OXO : MonoBehaviour
{
    [Serializable]
    public struct WinLine
    {
        public int a;
        public int b;
        public int c;
    }

    public GameObject[] spaces = new GameObject[9];
    public GameObject cam;
    public GameObject StartButton;
    public GameObject X;
    public GameObject O;
    public GameObject[] turnDisplay = new GameObject[2];
    public Canvas uiCanvas;                 // assign your Canvas in Inspector
    public float uiScale = 0.6f;            // baseline scale applied to UI prefab
    public float uiSizeMultiplier = 0.85f;  // fraction of space screen size to occupy
    public TextMeshProUGUI winsx;
    public TextMeshProUGUI winso;
    public TextMeshProUGUI xwins;
    public TextMeshProUGUI owins;
    public TextMeshProUGUI tie;

    private InputAction clickAction;

    // New: board model, 1 = X, -1 = O, 0 = empty
    private int[] board = new int[9];
    private bool gameOver = false;

    // automatic restart configuration (seconds)
    public float autoRestartDelay = 2f;
    private Coroutine autoRestartCoroutine;

    // transform tweaks for placed pieces (world prefabs)
    public Vector3 pieceLocalScale = Vector3.one * 0.3f; // adjust to fit your board
    public Vector3 pieceLocalEuler = Vector3.zero;       // local rotation for placed piece
    public float pieceLocalYOffset = 0.01f;              // small offset to avoid z-fighting

    // optional extra (custom) win lines you can set in the Inspector
    public WinLine[] extraWinLines;

    // Winning lines (rows, cols, diags)
    private readonly int[,] winLines = new int[,]
    {
        {0,1,2},
        {3,4,5},
        {6,7,8},
        {0,3,6},
        {1,4,7},
        {2,5,8},
        {0,4,8},
        {2,4,6}
    };

    private void Awake()
    {
        if (cam == null)
        {
            cam = Camera.main.gameObject;
        }

        // Default extra V-shaped win lines if none configured in Inspector:
        if (extraWinLines == null || extraWinLines.Length == 0)
        {
            extraWinLines = new WinLine[]
            {
                new WinLine { a = 0, b = 2, c = 4 }, // top caret (top-left, top-right, center)
                new WinLine { a = 6, b = 8, c = 4 }, // bottom v (bottom-left, bottom-right, center)
                new WinLine { a = 0, b = 6, c = 4 }, // left v (top-left, bottom-left, center)
                new WinLine { a = 2, b = 8, c = 4 }  // right v (top-right, bottom-right, center)
            };
        }

        // Create a click action for left mouse button / touch press
        clickAction = new InputAction(type: InputActionType.Button, binding: "<Pointer>/press");
    }
    private void OnEnable()
    {
        // Subscribe using the method that matches the expected callback signature
        clickAction.performed += OnClick;
        clickAction.Enable();
    }
    private void OnDisable()
    {
        clickAction.performed -= OnClick;
        clickAction.Disable();
    }
    private void OnClick(InputAction.CallbackContext context)
    {
        // If game ended, ignore further clicks
        if (gameOver)
            return;

        // Guard pointer availability
        if (Pointer.current == null)
            return;

        // Get pointer position (mouse or touch)
        Vector2 pointerPos = Pointer.current.position.ReadValue();

        // Raycast from camera to pointer position
        Camera camComp = cam != null ? cam.GetComponent<Camera>() : Camera.main;
        if (camComp == null)
            return;

        Ray ray = camComp.ScreenPointToRay(pointerPos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider is BoxCollider)
            {
                GameObject hitGO = hit.collider.gameObject;

                // Ensure turnDisplay indices exist and check their active state
                // 0 is X's turn, 1 is O's turn and because of the start button O starts first.
                bool xTurn = turnDisplay != null && turnDisplay.Length > 0 && turnDisplay[0] != null && turnDisplay[0].activeSelf;
                bool oTurn = turnDisplay != null && turnDisplay.Length > 1 && turnDisplay[1] != null && turnDisplay[1].activeSelf;

                if (hitGO.CompareTag("Space"))
                {
                    // Map clicked space to index in spaces[] (inspector must populate spaces in correct order)
                    int index = Array.IndexOf(spaces, hitGO);
                    if (index < 0)
                    {
                        Debug.LogWarning($"Clicked Space not found in spaces[] array: {hitGO.name}");
                        return;
                    }

                    // Occupied? ignore
                    if (board[index] != 0)
                    {
                        Debug.Log("Space already occupied.");
                        return;
                    }

                    // Place piece and update board using unified placer
                    if (xTurn)
                    {
                        Debug.Log("Placing X");
                        PlacePrefabAtSpace(X, hitGO);
                        board[index] = 1;
                        turnDisplay[0].SetActive(false);
                        turnDisplay[1].SetActive(true);
                    }
                    else if (oTurn)
                    {
                        Debug.Log("Placing O");
                        PlacePrefabAtSpace(O, hitGO);
                        board[index] = -1;
                        turnDisplay[1].SetActive(false);
                        turnDisplay[0].SetActive(true);
                    }
                    else
                    {
                        // No turn indicator active — ignore
                        Debug.Log("No active turn display. Ignoring click.");
                        return;
                    }

                    // Check win / tie
                    int result = CheckWin(); // 1 => X, -1 => O, 0 => no winner yet, 2 => tie
                    if (result == 1)
                    {
                        Debug.Log("X wins!");
                        if (xwins != null) xwins.gameObject.SetActive(true);
                        winsx.text = (int.Parse(winsx.text) + 1).ToString();
                        gameOver = true;
                        DisableTurnDisplays();
                        StartAutoRestart();
                    }
                    else if (result == -1)
                    {
                        Debug.Log("O wins!");
                        if (owins != null) owins.gameObject.SetActive(true);
                        winso.text = (int.Parse(winso.text) + 1).ToString();
                        gameOver = true;
                        DisableTurnDisplays();
                        StartAutoRestart();
                    }
                    else if (result == 2)
                    {
                        Debug.Log("Tie!");
                        if (tie != null) tie.gameObject.SetActive(true);
                        gameOver = true;
                        DisableTurnDisplays();
                        StartAutoRestart();
                    }
                }

                Debug.Log($"Clicked on: {hitGO.name}");
            }
        }
    }

    // Unified placer: handles UI prefabs (RectTransform) and world prefabs
    private GameObject PlacePrefabAtSpace(GameObject prefab, GameObject space)
    {
        if (prefab == null || space == null) return null;

        // Instantiate a copy first (no parent)
        GameObject inst = Instantiate(prefab);
        // If this prefab contains a RectTransform (UI prefab)
        RectTransform pieceRT = inst.GetComponentInChildren<RectTransform>();
        if (pieceRT != null && uiCanvas != null)
        {
            // parent to canvas (maintain prefab layout)
            inst.transform.SetParent(uiCanvas.transform, false);

            // convert world space center of the space to canvas local point
            RectTransform canvasRect = uiCanvas.GetComponent<RectTransform>();
            Vector2 screenPoint = Camera.main.WorldToScreenPoint(space.transform.position);
            Vector2 localPoint;
            Camera canvasCam = (uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : uiCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCam, out localPoint);

            pieceRT.anchoredPosition = localPoint;
            pieceRT.localScale = Vector3.one * uiScale;

            // If space has BoxCollider, compute approximate screen size and apply to sizeDelta
            BoxCollider box = space.GetComponent<BoxCollider>();
            if (box != null)
            {
                Vector3 worldMin = box.bounds.min;
                Vector3 worldMax = box.bounds.max;
                Vector2 screenA = Camera.main.WorldToScreenPoint(worldMin);
                Vector2 screenB = Camera.main.WorldToScreenPoint(worldMax);
                Vector2 screenSize = new Vector2(Mathf.Abs(screenB.x - screenA.x), Mathf.Abs(screenB.y - screenA.y));

                // apply multiplier so UI doesn't fully touch edges
                Vector2 desired = screenSize * uiSizeMultiplier;

                // sizeDelta is in canvas local units — for ScreenSpaceOverlay, pixels ~= units
                pieceRT.sizeDelta = desired;

                var tmp = inst.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    // Prefer automatic sizing so text fills the rect without manual math
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 180; // sensible lower bound
                    tmp.fontSizeMax = Mathf.Max(24, Mathf.RoundToInt(desired.y * 0.7f)); // upper bound heuristic
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.enableWordWrapping = false;
                    tmp.ForceMeshUpdate(); // apply immediately for diagnostics
                }
            }

            return inst;
        }

        // Otherwise treat as world object — parent to space and apply your existing transforms
        inst.transform.SetParent(space.transform, false);
        inst.transform.localPosition = Vector3.up * pieceLocalYOffset;
        inst.transform.localEulerAngles = pieceLocalEuler;
        inst.transform.localScale = pieceLocalScale;
        return inst;
    }

    private void DisableTurnDisplays()
    {
        if (turnDisplay != null)
        {
            if (turnDisplay.Length > 0 && turnDisplay[0] != null) turnDisplay[0].SetActive(false);
            if (turnDisplay.Length > 1 && turnDisplay[1] != null) turnDisplay[1].SetActive(false);
        }
    }

    private void StartAutoRestart()
    {
        // do not hide xwins here — keep it visible during the delay
        // prevent multiple coroutines
        if (autoRestartCoroutine != null)
        {
            StopCoroutine(autoRestartCoroutine);
            autoRestartCoroutine = null;
        }
        autoRestartCoroutine = StartCoroutine(AutoRestart(autoRestartDelay));
    }

    private IEnumerator AutoRestart(float delay)
    {
        // optional visual/audio/pause time for player to see result
        yield return new WaitForSeconds(delay);

        // hide any temporary win indicator(s) before resetting
        if (xwins != null) xwins.gameObject.SetActive(false);
        if (owins != null) owins.gameObject.SetActive(false);
        if (tie != null) tie.gameObject.SetActive(false);

        // reuse StartGame logic to reset board and clear pieces
        StartGame();

        // clear coroutine reference
        autoRestartCoroutine = null;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // initialize board
        for (int i = 0; i < board.Length; i++) board[i] = 0;
        gameOver = false;

        cam.GetComponent<AudioSource>().enabled = false;
        turnDisplay[0].SetActive(false);
        turnDisplay[1].SetActive(true);
        winsx.text = "0";
        winso.text = "0";
        winsx.gameObject.SetActive(false);
        winso.gameObject.SetActive(false);
        xwins.gameObject.SetActive(false);
        Debug.Log("O turn First!");
        if (StartButton != null)
        {
            StartButton.SetActive(true);
            // var btn because it needed to stored and then accessed again.
            var btn = StartButton.GetComponent<Button>();
            // not null because its checking if the button is active or not and if so start the game.
            if (btn != null)
            {
                btn.onClick.AddListener(StartGame);
            }
            else
            {
                Debug.LogError("StartButton does not have a Button component.");
            }
        }
    }

    public void StartGame()
    {
        // reset internal state in case of restart
        for (int i = 0; i < board.Length; i++) board[i] = 0;
        gameOver = false;

        // Optionally clear instantiated children under each space:
        foreach (var s in spaces)
        {
            if (s == null) continue;
            for (int c = s.transform.childCount - 1; c >= 0; c--)
            {
                Destroy(s.transform.GetChild(c).gameObject);
            }
        }

        // Also clear any UI children placed under the canvas that came from spaces
        if (uiCanvas != null)
        {
            // optional: if you want to clear only pieces created by this script you can tag them or keep references
            // quick approach removes any children that have prefab names "X" or "O"
            List<Transform> toDestroy = new List<Transform>();
            foreach (Transform ch in uiCanvas.transform)
            {
                if (ch.name.Contains(X != null ? X.name : "X") || ch.name.Contains(O != null ? O.name : "O"))
                    toDestroy.Add(ch);
            }
            foreach (var t in toDestroy) Destroy(t.gameObject);
        }

        cam.GetComponent<AudioSource>().enabled = true;
        turnDisplay[0].SetActive(false);
        turnDisplay[1].SetActive(true);
        winso.gameObject.SetActive(true);
        winsx.gameObject.SetActive(true);
        if (StartButton != null)
        {
            // Deactivate the button's GameObject when clicked
            StartButton.SetActive(false);
        }
    }

    // returns 1 if X wins, -1 if O wins, 2 for tie, 0 for no winner yet
    private int CheckWin()
    {
        for (int line = 0; line < winLines.GetLength(0); line++)
        {
            int a = winLines[line, 0];
            int b = winLines[line, 1];
            int c = winLines[line, 2];
            int sum = board[a] + board[b] + board[c];
            if (sum == 3) return 1;
            if (sum == -3) return -1;
        }

        // check extra custom win lines (V shapes etc.)
        if (extraWinLines != null)
        {
            for (int i = 0; i < extraWinLines.Length; i++)
            {
                int a = extraWinLines[i].a;
                int b = extraWinLines[i].b;
                int c = extraWinLines[i].c;
                // validate indices
                if (a < 0 || a >= board.Length || b < 0 || b >= board.Length || c < 0 || c >= board.Length) continue;
                int sum = board[a] + board[b] + board[c];
                if (sum == 3) return 1;
                if (sum == -3) return -1;
            }
        }

        // tie?
        bool hasEmpty = false;
        for (int i = 0; i < board.Length; i++)
        {
            if (board[i] == 0) { hasEmpty = true; break; }
        }
        if (!hasEmpty) return 2;

        return 0;
    }

    // Update is called once per frame
    void Update()
    {

    }
}