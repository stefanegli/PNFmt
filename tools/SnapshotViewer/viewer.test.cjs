// Run after exporting: node --test tools/SnapshotViewer/viewer.test.cjs
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

// Exercise the page's real script with a small DOM stand-in. No browser or dependencies needed.
class Element {
    constructor(tag) {
        this.tag = tag;
        this.children = [];
        this.dataset = {};
        this.attributes = {};
        this.value = "";
        this.checked = false;
    }
    set textContent(value) {
        this.text = value;
        this.children = [];
    }
    get textContent() {
        return (
            (this.text || "") +
            this.children.map((child) => child.textContent).join("")
        );
    }
    append(...children) {
        this.children.push(
            ...children.flatMap((child) =>
                child.tag === "fragment" ? child.children : [child],
            ),
        );
    }
    replaceChildren(...children) {
        this.children = [];
        this.text = "";
        this.append(...children);
    }
    setAttribute(name, value) {
        this.attributes[name] = value;
    }
}
function load() {
    const html = fs.readFileSync(
        path.join(__dirname, "../../artifacts/snapshot-review.html"),
        "utf8",
    );
    const elements = new Map(
        [...html.matchAll(/id="([^"]+)"/g)].map((match) => [
            match[1],
            new Element("div"),
        ]),
    );
    const context = vm.createContext({
        document: {
            getElementById: (id) => elements.get(id),
            createElement: (tag) => new Element(tag),
            createDocumentFragment: () => new Element("fragment"),
            createTextNode: (text) => {
                const item = new Element("text");
                item.textContent = text;
                return item;
            },
        },
    });
    const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
    vm.runInContext(script, context);
    return { context, elements, cases: vm.runInContext("data.cases", context) };
}

test("line comparison retains all input and output lines in every exported snapshot", () => {
    const { context, cases } = load();
    for (const item of cases) {
        const rows = context.compare(item.input, item.result);
        for (const [side, text] of [
            ["left", item.input],
            ["right", item.result],
        ]) {
            const values = Array.from(rows)
                .filter((row) => row[side] && row[side].num !== "")
                .map((row) => row[side].text);
            const normalized = text.replace(/\r\n?/g, "\n");
            const expected = normalized === "" ? [] : normalized.split("\n");
            if (normalized.endsWith("\n")) expected.pop();
            assert.deepEqual(values, expected, `${item.id}: ${side}`);
        }
    }
    const unchanged = context.compare("a\nb\n", "a\nb\n");
    assert.equal(unchanged.filter((row) => row.changed).length, 0);
    const finalNewline = context.compare("a", "a\n");
    assert.equal(finalNewline.length, 2);
    assert.equal(finalNewline[1].changed, true);
});

test("provider selection, navigation, search, and display controls work together", () => {
    const { elements, cases } = load();
    const get = (id) => elements.get(id);
    assert.equal(get("tests").children.length, cases.length);
    for (const provider of new Set(cases.map((item) => item.provider))) {
        get("provider").value = provider;
        get("provider").onchange();
        assert.equal(
            get("tests").children.length,
            cases.filter((item) => item.provider === provider).length,
        );
        assert.ok(get("category").textContent.startsWith(provider));
    }
    get("provider").value = "Resource";
    get("provider").onchange();
    const first = get("output-path").textContent;
    get("next").onclick();
    assert.notEqual(get("output-path").textContent, first);
    get("prev").onclick();
    assert.equal(get("output-path").textContent, first);
    get("search").value = "config1/Sort";
    get("search").oninput();
    assert.equal(get("tests").children.length, 1);
    assert.equal(get("title").textContent, "config1/Sort.resx");
    get("changes").checked = true;
    get("changes").onchange();
    const changedRows = get("rows").children.length;
    get("changes").checked = false;
    get("changes").onchange();
    assert.ok(get("rows").children.length >= changedRows);
    get("whitespace").checked = true;
    get("whitespace").onchange();
    assert.ok(get("rows").textContent.includes("·"));
    get("search").value = "no such snapshot exists";
    get("search").oninput();
    assert.equal(get("detail").hidden, true);
    assert.equal(get("no-tests").hidden, false);
    get("search").value = "";
    get("search").oninput();
    assert.equal(get("detail").hidden, false);
});
