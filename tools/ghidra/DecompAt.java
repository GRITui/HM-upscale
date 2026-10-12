// Headless helper: decompile functions containing given addresses, or list string xrefs.
// Usage: -postScript DecompAt.java <outfile> <cmd> args...
//   cmd = str <substring>   : find strings containing substring, print referencing functions and decompile them
//   cmd = at <hexaddr>...   : decompile function containing each address
//   cmd = dis <hexaddr> <n> : disassemble n instructions
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.data.*;
import java.io.*;
import java.util.*;

public class DecompAt extends GhidraScript {
    PrintWriter out;
    DecompInterface di;
    Set<Address> done = new HashSet<>();
    void dec(Function f) throws Exception {
        if (f == null || !done.add(f.getEntryPoint())) return;
        out.println("\n=== " + f.getName() + " @ " + f.getEntryPoint() + " ===");
        DecompileResults r = di.decompileFunction(f, 60, monitor);
        if (r.decompileCompleted()) out.println(r.getDecompiledFunction().getC());
        else out.println("decompile failed");
    }
    public void run() throws Exception {
        String[] a = getScriptArgs();
        out = new PrintWriter(new FileWriter(a[0]));
        di = new DecompInterface(); di.openProgram(currentProgram);
        String cmd = a[1];
        if (cmd.equals("str")) {
            DataIterator it = currentProgram.getListing().getDefinedData(true);
            while (it.hasNext()) {
                Data d = it.next();
                if (!(d.getValue() instanceof String)) continue;
                String s = (String) d.getValue();
                if (!s.toLowerCase().contains(a[2].toLowerCase())) continue;
                out.println("STRING " + d.getAddress() + " " + s);
                for (Reference r : getReferencesTo(d.getAddress())) {
                    out.println("  xref " + r.getFromAddress());
                    dec(getFunctionContaining(r.getFromAddress()));
                }
            }
        } else if (cmd.equals("findstr")) {
            byte[] pat = a[2].getBytes();
            Address ad = currentProgram.getMinAddress();
            while (true) {
                ad = currentProgram.getMemory().findBytes(ad, pat, null, true, monitor);
                if (ad == null) break;
                out.println("STR @ " + ad);
                for (Reference r : getReferencesTo(ad)) { out.println("  xref " + r.getFromAddress() + " in " + getFunctionContaining(r.getFromAddress())); if (a.length > 3) dec(getFunctionContaining(r.getFromAddress())); }
                ad = ad.add(1);
            }
        } else if (cmd.equals("scalar")) {
            long v = Long.decode(a[2]);
            InstructionIterator ii = currentProgram.getListing().getInstructions(true);
            while (ii.hasNext()) { Instruction in = ii.next();
                for (int k = 0; k < in.getNumOperands(); k++) { ghidra.program.model.scalar.Scalar sc = in.getScalar(k); if (sc != null && sc.getUnsignedValue() == v) { out.println(in.getAddress() + " " + in + " in " + getFunctionContaining(in.getAddress())); if (a.length > 3) dec(getFunctionContaining(in.getAddress())); } } }
        } else if (cmd.equals("at")) {
            for (int i = 2; i < a.length; i++) dec(getFunctionContaining(toAddr(a[i])));
        } else if (cmd.equals("dis")) {
            Address ad = toAddr(a[2]); int n = Integer.parseInt(a[3]);
            for (int i = 0; i < n; i++) { Instruction in = getInstructionAt(ad); if (in == null) break; out.println(ad + "  " + in); ad = in.getNext() == null ? ad.add(4) : in.getNext().getAddress(); }
        } else if (cmd.equals("xref")) {
            for (Reference r : getReferencesTo(toAddr(a[2]))) { out.println("xref " + r.getFromAddress() + " in " + getFunctionContaining(r.getFromAddress())); }
        }
        out.close();
    }
}
