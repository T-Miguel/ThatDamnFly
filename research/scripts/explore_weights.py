import pyarrow as pa, pyarrow.feather as pf, pyarrow.ipc as ipc, pyarrow.compute as pc, pandas as pd, time
pd.set_option("display.width", 220)
t0=time.time()
src = pa.memory_map("data/raw/connectome-weights-male-cns-v1.0-minconf-0.5.feather")
reader = ipc.open_file(src)
print("schema:", reader.schema); print("num_record_batches:", reader.num_record_batches)
b0 = reader.get_batch(0); print("batch0 rows:", b0.num_rows); print(b0.slice(0,5).to_pandas().to_string())
print("elapsed", round(time.time()-t0,1))
