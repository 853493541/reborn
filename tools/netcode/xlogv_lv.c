#include <io.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* JX3 xlogv replacement: the client spawns
 *   xlogv -i <fd1_read>  -o <fd2_write>
 * so -i is the input stream (the client's stdout, where KGLog writes) and -o is
 * the output stream (the client's stdin). Read -i, append to the dump file. */
int main(int argc, char** argv) {
    int rfd = -1;
    int wfd = -1;
    int i;
    FILE* f;
    for (i = 1; i < argc; i++) {
        if (strcmp(argv[i], "-i") == 0 && i + 1 < argc) rfd = atoi(argv[++i]);
        else if (strcmp(argv[i], "-o") == 0 && i + 1 < argc) wfd = atoi(argv[++i]);
    }
    f = fopen("C:\\jx3tmp\\client_log.txt", "ab");
    if (!f) return 1;
    fprintf(f, "== lv start rfd=%d wfd=%d argc=%d ==\n", rfd, wfd, argc);
    fflush(f);
    if (rfd >= 0) {
        char buf[8192];
        int n;
        while ((n = _read(rfd, buf, sizeof(buf))) > 0) {
            fwrite(buf, 1, n, f);
            fflush(f);
        }
        fprintf(f, "\n== lv read ended n=%d ==\n", n);
    } else {
        fprintf(f, "== lv: no read fd ==\n");
    }
    fclose(f);
    return 0;
}
